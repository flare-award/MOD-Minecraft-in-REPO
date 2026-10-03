// TCP client that talks to the Fabric mod running inside Minecraft.
//
// Threading model:
//   * one reader thread per connection; it never touches Unity objects and
//     instead queues callbacks into mainQueue, which Update() drains on the
//     main thread;
//   * writes happen from the main thread under writeLock.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using BepInEx.Logging;
using UnityEngine;

namespace MinecraftInRepo.Net
{
    public sealed class BridgeClient : MonoBehaviour
    {
        private ModConfig config;
        private ManualLogSource log;

        private TcpClient tcp;
        private NetworkStream stream;
        private StreamWriter writer;
        private readonly object writeLock = new object();
        private Thread readerThread;

        private volatile bool connected;
        private bool connecting;
        private float nextConnectAttempt;
        private float nextPing;

        private readonly ConcurrentQueue<Action> mainQueue = new ConcurrentQueue<Action>();

        public bool Connected => connected;

        /// <summary>Raised on the main thread with the Minecraft/bridge version string.</summary>
        public event Action<string> HelloReceived;

        /// <summary>Raised on the main thread for every explosion reported by Minecraft.</summary>
        public event Action<McExplosion> ExplosionReceived;

        /// <summary>Raised on the main thread when Minecraft answers a getpos request.</summary>
        public event Action<McPose> PosReceived;

        public void Init(ModConfig modConfig, ManualLogSource logger)
        {
            config = modConfig;
            log = logger;
        }

        private void Update()
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
                    log.LogError("[MinecraftInRepo] Bridge event error: " + e);
                }
            }

            if (!connected && !connecting && Time.time >= nextConnectAttempt)
            {
                StartCoroutine(ConnectRoutine());
            }

            if (connected && Time.time >= nextPing)
            {
                nextPing = Time.time + Mathf.Max(0.5f, config.PingSeconds.Value);
                SendRaw(JsonLite.WriteObject("t", "ping"));
            }
        }

        private IEnumerator ConnectRoutine()
        {
            // NB: C# forbids `yield return` inside a try/catch, so the wait
            // loop lives outside of it.
            connecting = true;
            bool success = false;
            TcpClient client = new TcpClient();
            IAsyncResult ar = null;
            try
            {
                ar = client.BeginConnect(IPAddress.Loopback, config.Port.Value, null, null);
            }
            catch (Exception e)
            {
                log.LogInfo("[MinecraftInRepo] Connection attempt failed: " + e.Message);
            }

            if (ar != null)
            {
                float deadline = Time.realtimeSinceStartup + 2f;
                while (!ar.IsCompleted && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                try
                {
                    if (!ar.IsCompleted)
                    {
                        try { client.Close(); } catch { /* ignore */ }
                        log.LogInfo("[MinecraftInRepo] Minecraft bridge not reachable (is Minecraft running?). Retrying.");
                    }
                    else
                    {
                        client.EndConnect(ar);
                        client.NoDelay = true;
                        tcp = client;
                        stream = client.GetStream();
                        writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = false };
                        connected = true;
                        nextPing = Time.time + config.PingSeconds.Value;

                        readerThread = new Thread(ReaderLoop) { IsBackground = true, Name = "MinecraftInRepo-BridgeReader" };
                        readerThread.Start(stream);

                        SendRaw(JsonLite.WriteObject("t", "hello", "proto", 1));
                        log.LogInfo("[MinecraftInRepo] Connected to the Minecraft bridge on port " + config.Port.Value);
                        success = true;
                    }
                }
                catch (Exception e)
                {
                    try { client.Close(); } catch { /* ignore */ }
                    log.LogInfo("[MinecraftInRepo] Connection attempt failed: " + e.Message);
                }
            }

            connecting = false;
            if (!success)
            {
                nextConnectAttempt = Time.time + Mathf.Max(0.5f, config.ConnectInterval.Value);
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
                // socket closed; handled below
            }
            mainQueue.Enqueue(() => HandleDisconnect());
        }

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
            switch (type)
            {
                case "hello":
                {
                    string mc = JsonLite.GetString(msg, "mc", "?");
                    string bridge = JsonLite.GetString(msg, "bridge", "?");
                    if (HelloReceived != null)
                    {
                        HelloReceived("Minecraft " + mc + " (bridge " + bridge + ")");
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
                            Fire = msg.ContainsKey("fire") && (msg["fire"] is bool fireFlag ? fireFlag : JsonLite.GetString(msg, "fire") == "true"),
                            Source = JsonLite.GetString(msg, "source", "generic"),
                        });
                    }
                    break;
                }
                case "pos":
                {
                    if (PosReceived != null)
                    {
                        PosReceived(new McPose
                        {
                            X = JsonLite.GetNumber(msg, "x"),
                            Y = JsonLite.GetNumber(msg, "y"),
                            Z = JsonLite.GetNumber(msg, "z"),
                            Yaw = (float)JsonLite.GetNumber(msg, "yaw"),
                            Pitch = (float)JsonLite.GetNumber(msg, "pitch"),
                        });
                    }
                    break;
                }
                case "pong":
                    break;
            }
        }

        private void HandleDisconnect()
        {
            if (!connected)
            {
                return;
            }
            connected = false;
            CloseSocket();
            nextConnectAttempt = Time.time + Mathf.Max(0.5f, config.ConnectInterval.Value);
            log.LogInfo("[MinecraftInRepo] Minecraft bridge disconnected.");
        }

        /// <summary>Sends the current camera pose to Minecraft (called at most SendRateHz times per second).</summary>
        public void SendCam(double x, double y, double z, float yaw, float pitch, float fov)
        {
            SendRaw(JsonLite.WriteObject(
                "t", "cam",
                "x", Round(x),
                "y", Round(y),
                "z", Round(z),
                "yaw", Round(yaw),
                "pitch", Round(pitch),
                "fov", Round(fov)));
        }

        /// <summary>Asks Minecraft for its current player pose (used by F7 calibration).</summary>
        public void SendGetPos()
        {
            SendRaw(JsonLite.WriteObject("t", "getpos"));
        }

        private static double Round(double value)
        {
            return Math.Round(value * 1000.0) / 1000.0;
        }

        private void SendRaw(string line)
        {
            if (!connected || writer == null)
            {
                return;
            }
            try
            {
                lock (writeLock)
                {
                    writer.WriteLine(line);
                    writer.Flush();
                }
            }
            catch (Exception)
            {
                connected = false;
                CloseSocket();
                nextConnectAttempt = Time.time + Mathf.Max(0.5f, config.ConnectInterval.Value);
            }
        }

        private void CloseSocket()
        {
            try { if (tcp != null) tcp.Close(); } catch { /* ignore */ }
            tcp = null;
            stream = null;
            writer = null;
        }
    }
}
