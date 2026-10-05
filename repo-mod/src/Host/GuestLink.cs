// The v2 link between R.E.P.O. (host) and Minecraft (guest).
//
// Transport: newline-delimited JSON over TCP loopback - the same transport the
// v1 bridge already uses, so nothing new has to be installed and the traffic
// stays debuggable with a text editor. See docs/PROTOCOL-V2.md for the messages.
//
// Threading: one reader thread per connection enqueues lines; Tick() drains the
// queue on the main thread (called from the plugin's render pump - in R.E.P.O.
// MonoBehaviour messages are not delivered to plugin components, so there are
// no coroutines anywhere in this file).
//
// Free of UnityEngine and BepInEx types: unit testable without the game.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MinecraftInRepo.Net;

namespace MinecraftInRepo.Host
{
    public sealed class GuestLink
    {
        public const int ProtocolVersion = 2;

        /// <summary>The guest counts as alive while its stamp is younger than this.</summary>
        public const int GuestTimeoutMs = 3000;

        private readonly Action<string> log;
        private readonly IPAddress address;
        private readonly int port;

        private TcpClient tcp;
        private NetworkStream stream;
        private StreamWriter writer;
        private readonly object writeLock = new object();
        private Thread readerThread;
        private readonly ConcurrentQueue<Action> mainQueue = new ConcurrentQueue<Action>();

        private volatile bool connected;
        private bool connecting;
        private int nextConnectAttemptMs;
        private int lastGuestStampMs;
        private bool started;

        // Connect state machine (polled, never a coroutine).
        private TcpClient pendingClient;
        private IAsyncResult pendingResult;
        private int connectDeadlineMs;

        public GuestLink(Action<string> log, string host, int port)
        {
            this.log = log ?? delegate { };
            this.address = string.IsNullOrEmpty(host) ? IPAddress.Loopback : IPAddress.Parse(host);
            this.port = port;
            Enabled = true;
        }

        /// <summary>When false the link stays idle and the host plays unmodified.</summary>
        public bool Enabled { get; set; }

        public bool Connected => connected;

        /// <summary>Connected and the guest's heartbeat is fresh.</summary>
        public bool LinkUp => connected && lastGuestStampMs != 0 && ElapsedSince(lastGuestStampMs) < GuestTimeoutMs;

        public GuestState Guest { get; private set; }

        /// <summary>Milliseconds since the guest's last state message; -1 if never.</summary>
        public int GuestAgeMs => lastGuestStampMs == 0 ? -1 : ElapsedSince(lastGuestStampMs);

        /// <summary>Set by the guest's hello; a change means a new guest process.</summary>
        public string SessionId { get; private set; }

        public int Protocol => guestProtocol;
        private int guestProtocol;

        public long LastTeleportSeq { get; private set; }

        /// <summary>Last host state sequence number that was published.</summary>
        public long HostSeq => hostSeq;

        /// <summary>Raised on the main thread when the guest reports an explosion (TNT routing).</summary>
        public event Action<McExplosion> ExplosionReceived;

        /// <summary>Raised on the main thread for guest events: "death", "respawn", "screen".</summary>
        public event Action<string> GuestEvent;

        /// <summary>Raised on the main thread when the guest introduces itself.</summary>
        public event Action<string> HelloReceived;

        public void Start()
        {
            started = true;
            nextConnectAttemptMs = Now();
        }

        public void Stop()
        {
            started = false;
            CloseSocket();
        }

        // ---------------------------------------------------------------- tick

        public void Tick()
        {
            Action action;
            while (mainQueue.TryDequeue(out action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    log("[MinecraftInRepo] guest link event error: " + e.Message);
                }
            }

            if (!started || !Enabled)
            {
                if (connected)
                {
                    CloseSocket();
                }
                return;
            }

            if (connecting)
            {
                PollConnect();
            }
            else if (!connected && Now() >= nextConnectAttemptMs)
            {
                StartConnect();
            }
        }

        private static int Now()
        {
            return Environment.TickCount;
        }

        /// <summary>Milliseconds since a TickCount stamp, correct across the 49-day wrap.</summary>
        private static int ElapsedSince(int stamp)
        {
            return unchecked(Now() - stamp);
        }

        // ----------------------------------------------------------- connecting

        private void StartConnect()
        {
            connecting = true;
            pendingClient = new TcpClient();
            try
            {
                pendingResult = pendingClient.BeginConnect(address, port, null, null);
                connectDeadlineMs = unchecked(Now() + 2000);
            }
            catch (Exception e)
            {
                log("[MinecraftInRepo] guest connect failed: " + e.Message);
                ConnectFinished(false);
            }
        }

        private void PollConnect()
        {
            bool completed = pendingResult != null && pendingResult.IsCompleted;
            if (!completed && ElapsedSince(connectDeadlineMs) < 0)
            {
                return;
            }

            TcpClient client = pendingClient;
            if (!completed)
            {
                try { if (client != null) client.Close(); } catch { /* ignore */ }
                log("[MinecraftInRepo] no Minecraft guest on port " + port + " - retrying.");
                ConnectFinished(false);
                return;
            }

            try
            {
                client.EndConnect(pendingResult);
                client.NoDelay = true;
                tcp = client;
                stream = client.GetStream();
                writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = false };
                connected = true;
                lastGuestStampMs = 0;
                SessionId = null;
                guestProtocol = 0;

                readerThread = new Thread(ReaderLoop) { IsBackground = true, Name = "MinecraftInRepo-GuestReader" };
                readerThread.Start(stream);

                SendRaw(JsonLite.WriteObject("t", "hello", "proto", ProtocolVersion, "game", "REPO"));
                log("[MinecraftInRepo] guest link connected on port " + port + ".");
                ConnectFinished(true);
            }
            catch (Exception e)
            {
                try { client.Close(); } catch { /* ignore */ }
                log("[MinecraftInRepo] guest connect failed: " + e.Message);
                ConnectFinished(false);
            }
        }

        private void ConnectFinished(bool success)
        {
            connecting = false;
            pendingClient = null;
            pendingResult = null;
            if (!success)
            {
                nextConnectAttemptMs = unchecked(Now() + 2000);
            }
        }

        private void ReaderLoop(object state)
        {
            NetworkStream s = (NetworkStream)state;
            try
            {
                StreamReader reader = new StreamReader(s, Encoding.UTF8);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string copy = line;
                    mainQueue.Enqueue(() => HandleLine(copy));
                }
            }
            catch (Exception)
            {
                // socket closed; the disconnect is reported below
            }
            mainQueue.Enqueue(HandleDisconnect);
        }

        private void HandleDisconnect()
        {
            if (!connected)
            {
                return;
            }
            log("[MinecraftInRepo] guest link lost (Minecraft closed or hung).");
            CloseSocket();
        }

        private void CloseSocket()
        {
            connected = false;
            connecting = false;
            lastGuestStampMs = 0;
            SessionId = null;
            try { if (writer != null) writer.Close(); } catch { /* ignore */ }
            try { if (stream != null) stream.Close(); } catch { /* ignore */ }
            try { if (tcp != null) tcp.Close(); } catch { /* ignore */ }
            writer = null;
            stream = null;
            tcp = null;
        }

        // ------------------------------------------------------------- inbound

        private void HandleLine(string line)
        {
            Dictionary<string, object> msg;
            try
            {
                msg = JsonLite.ParseObject(line);
            }
            catch (Exception)
            {
                return;
            }

            string type = JsonLite.GetString(msg, "t");
            if (string.IsNullOrEmpty(type))
            {
                type = JsonLite.GetString(msg, "type");
            }

            switch (type)
            {
                case "hello":
                {
                    guestProtocol = (int)JsonLite.GetNumber(msg, "proto");
                    string session = JsonLite.GetString(msg, "session", "?");
                    bool fresh = SessionId != null && SessionId != session;
                    SessionId = session;
                    if (HelloReceived != null)
                    {
                        HelloReceived(JsonLite.GetString(msg, "mc", "?") + " (proto " + guestProtocol +
                                      (fresh ? ", new session" : "") + ")");
                    }
                    break;
                }
                case "gs":
                {
                    GuestState state;
                    if (GuestState.TryParse(msg, out state))
                    {
                        Guest = state;
                        lastGuestStampMs = Now();
                    }
                    break;
                }
                case "boom":
                {
                    if (ExplosionReceived != null)
                    {
                        ExplosionReceived(new McExplosion
                        {
                            X = JsonLite.GetNumber(msg, "x"),
                            Y = JsonLite.GetNumber(msg, "y"),
                            Z = JsonLite.GetNumber(msg, "z"),
                            Power = (float)JsonLite.GetNumber(msg, "power", 4.0),
                            Fire = JsonLite.GetBool(msg, "fire"),
                            Source = JsonLite.GetString(msg, "source", "tnt")
                        });
                    }
                    break;
                }
                case "ev":
                {
                    if (GuestEvent != null)
                    {
                        GuestEvent(JsonLite.GetString(msg, "kind", "?"));
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------- outbound

        private void SendRaw(string line)
        {
            if (!connected)
            {
                return;
            }
            lock (writeLock)
            {
                try
                {
                    writer.Write(line);
                    writer.Write('\n');
                    writer.Flush();
                }
                catch (Exception)
                {
                    // the reader thread reports the disconnect
                }
            }
        }

        /// <summary>Publish the host state. Called once per frame; the guest paces on "seq".</summary>
        public void Publish(HostState state)
        {
            state.Seq = ++hostSeq;
            state.TimestampMs = Now();
            LastTeleportSeq = state.TeleportSeq;
            SendRaw(state.ToJson());
        }

        private long hostSeq;

        public void SendKey(int glfwCode, bool down, bool repeat)
        {
            SendRaw(JsonLite.WriteObject("t", "key", "code", glfwCode, "down", down, "rep", repeat));
        }

        public void SendMouseButton(int button, bool down)
        {
            SendRaw(JsonLite.WriteObject("t", "mbtn", "btn", button, "down", down));
        }

        public void SendLook(float dx, float dy)
        {
            SendRaw(JsonLite.WriteObject("t", "look", "dx", dx, "dy", dy));
        }

        public void SendScroll(float dx, float dy)
        {
            SendRaw(JsonLite.WriteObject("t", "scroll", "dx", dx, "dy", dy));
        }

        public void SendText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            SendRaw(JsonLite.WriteObject("t", "char", "s", text));
        }

        public void SendCursor(float x, float y)
        {
            SendRaw(JsonLite.WriteObject("t", "cursor", "x", x, "y", y));
        }

        /// <summary>Release every key and button the guest thinks is held.</summary>
        public void SendReleaseAll()
        {
            SendRaw(JsonLite.WriteObject("t", "release"));
        }

        public void SendVoxel(VoxelRegion region, int epoch)
        {
            SendRaw(region.ToJson(epoch));
        }

        public void SendVoxelClear(int epoch)
        {
            SendRaw("{\"t\":\"voxclear\",\"epoch\":" + epoch.ToString(CultureInfo.InvariantCulture) + "}");
        }

        public void SendHurt(float amount, string kind)
        {
            SendRaw(JsonLite.WriteObject("t", "hurt", "amount", amount, "kind", kind ?? "hazard"));
        }

        public void SendCommand(string op, string argName = null, string argValue = null)
        {
            if (argName == null)
            {
                SendRaw(JsonLite.WriteObject("t", "cmd", "op", op));
            }
            else
            {
                SendRaw(JsonLite.WriteObject("t", "cmd", "op", op, argName, argValue));
            }
        }
    }
}
