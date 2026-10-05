// MinecraftInRepo - puts real Minecraft inside R.E.P.O.
//
//   * Minecraft's camera follows the R.E.P.O. camera (CameraSync -> bridge ->
//     Fabric mod moves the Minecraft player).
//   * The real Minecraft window is captured and drawn inside R.E.P.O.
//     (MinecraftCapture + MinecraftOverlay).
//   * TNT (and friends) exploding in Minecraft damages enemies, items,
//     valuables and players in R.E.P.O. (bridge -> ExplosionRouter).
//
// Hotkeys: F6 toggle camera follow, F7 calibrate world alignment,
//          F8 cycle overlay mode, F9 toggle blast damage.

using System.Threading;
using BepInEx;
using BepInEx.Logging;
using MinecraftInRepo.Blast;
using MinecraftInRepo.Capture;
using MinecraftInRepo.Host;
using MinecraftInRepo.Net;
using MinecraftInRepo.Overlay;
using MinecraftInRepo.Sync;
using UnityEngine;

namespace MinecraftInRepo
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class MinecraftInRepoPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.flareaward.minecraftinrepo";
        public const string PluginName = "Minecraft in R.E.P.O.";
        public const string PluginVersion = "1.1.0";

        internal static MinecraftInRepoPlugin Instance { get; private set; }

        private ModConfig config;
        private PlayModeGate gate;
        private CoordinateMap map;
        private BridgeClient bridge;
        private CameraSync cameraSync;
        private MinecraftCapture capture;
        private MinecraftOverlay overlay;
        private ExplosionRouter router;

        // Host/guest link (protocol v2). Null unless [HostGuest] Enabled is true,
        // in which case the mod behaves exactly as before.
        private GuestLink guestLink;
        private Ownership ownership;
        private HostState hostState;
        private long teleportSeq;
        private bool handoffRequested;
        private float nextGuestSummary;

        /// <summary>Distance from the host camera down to the character's feet.
        /// A placeholder until the phase-0 recon measures the real value.</summary>
        private const float HostEyeToFeet = 1.7f;

        private bool loggedFirstUpdate;

        private void Awake()
        {
            Instance = this;
            config = new ModConfig(Config);
            map = new CoordinateMap(config);

            // Put every component on BepInEx's own manager object: it is already
            // DontDestroyOnLoad and - unlike a GameObject created by a plugin
            // during chainloader startup - it is guaranteed to be ticked by
            // Unity, so Update/OnGUI/coroutines actually run.
            GameObject host = gameObject;
            Object.DontDestroyOnLoad(host);

            gate = host.AddComponent<PlayModeGate>();
            gate.Init(config, Logger);

            bridge = host.AddComponent<BridgeClient>();
            bridge.Init(config, Logger);

            router = host.AddComponent<ExplosionRouter>();
            router.Init(config, map, gate, Logger);

            cameraSync = host.AddComponent<CameraSync>();
            cameraSync.Init(config, map, bridge, gate, Logger);

            capture = host.AddComponent<MinecraftCapture>();
            capture.Init(config, gate, Logger);

            overlay = host.AddComponent<MinecraftOverlay>();
            overlay.Init(config, capture, bridge, router, cameraSync, gate, Logger);

            bridge.ExplosionReceived += router.Enqueue;
            bridge.PosReceived += cameraSync.OnMcPose;
            bridge.HelloReceived += version =>
                Logger.LogInfo("[MinecraftInRepo] Minecraft says hello: " + version);

            if (config.HostGuestMode.Value)
            {
                guestLink = new GuestLink(message => Logger.LogInfo("[MinecraftInRepo] " + message),
                    "127.0.0.1", config.GuestPort.Value);
                guestLink.ExplosionReceived += router.Enqueue;
                guestLink.GuestEvent += kind =>
                    Logger.LogInfo("[MinecraftInRepo] guest event: " + kind);
                guestLink.HelloReceived += version =>
                    Logger.LogInfo("[MinecraftInRepo] guest says hello: " + version);
                guestLink.Start();
                ownership = new Ownership();
                Logger.LogInfo("[MinecraftInRepo] Host/guest link armed on port " +
                    config.GuestPort.Value + " (protocol v2, docs/PROTOCOL-V2.md). " +
                    "Phase 1: link and ownership only - the body stays with R.E.P.O.");
            }

            Logger.LogInfo("[MinecraftInRepo] Keyboard backend: " + InputHelper.Backend);
            Logger.LogInfo("[MinecraftInRepo] Host object '" + host.name + "' scene=" + host.scene.name +
                " active=" + host.activeInHierarchy + " enabled=" + enabled);

            // R.E.P.O. does not deliver Update()/OnGUI() to plugin components, so
            // the mod is driven from Unity's render callback instead. This runs on
            // the main thread every frame, independently of MonoBehaviour messages.
            Application.onBeforeRender += Pump;
            StartHeartbeat();
            Logger.LogInfo(string.Format(
                "[MinecraftInRepo] {0} v{1} loaded. Waiting for Minecraft (bridge port {2}). " +
                "Hotkeys: F6 camera follow, F7 calibrate, F8 overlay, F9 TNT damage. " +
                (config.SinglePlayerOnly.Value
                    ? "Mode: solo only (the mod idles in multiplayer sessions)."
                    : "Mode: solo + co-op (SinglePlayerOnly=false)."),
                PluginName, PluginVersion, config.Port.Value));
        }

        private int lastTickFrame = -1;
        private int lastDrawFrame = -1;
        private bool loggedPump;
        private bool driverRan;
        private ModDriver driver;
        private float nextDriverCheck;

        private void Update()
        {
            TickAll();
        }

        /// <summary>Main-thread pump: keeps the mod alive even when Unity never
        /// delivers Update() to the plugin component.</summary>
        private void Pump()
        {
            if (!loggedPump)
            {
                loggedPump = true;
                Logger.LogInfo("[MinecraftInRepo] Render pump running (driving the mod from Application.onBeforeRender).");
            }

            if (!driverRan && Time.unscaledTime >= nextDriverCheck)
            {
                nextDriverCheck = Time.unscaledTime + 3f;
                EnsureDriver();
            }

            if (!driverRan)
            {
                TickAll();
            }
        }

        /// <summary>Creates a component that (unlike the plugin) does get OnGUI.</summary>
        private void EnsureDriver()
        {
            if (driverRan)
            {
                return;
            }
            if (driver != null)
            {
                // Created but never ticked - try once more on a fresh object.
                Object.Destroy(driver.gameObject);
                driver = null;
            }
            GameObject go = new GameObject("MinecraftInRepo_Driver");
            Object.DontDestroyOnLoad(go);
            driver = go.AddComponent<ModDriver>();
            driver.Init(this);
            Logger.LogInfo("[MinecraftInRepo] Driver component created (scene=" + go.scene.name + ").");
        }

        internal void TickFromDriver()
        {
            if (!driverRan)
            {
                driverRan = true;
                Logger.LogInfo("[MinecraftInRepo] Driver component is ticking - Update/OnGUI are delivered to it.");
            }
            TickAll();
        }

        internal void DrawFromDriver()
        {
            if (Time.frameCount == lastDrawFrame)
            {
                return;
            }
            lastDrawFrame = Time.frameCount;
            overlay.Draw();
        }

        /// <summary>Everything that has to happen once per frame, from whichever
        /// source manages to tick (plugin Update, render pump or driver).</summary>
        private void TickAll()
        {
            if (Time.frameCount == lastTickFrame)
            {
                return;
            }
            lastTickFrame = Time.frameCount;

            if (!loggedFirstUpdate)
            {
                loggedFirstUpdate = true;
                Logger.LogInfo("[MinecraftInRepo] Tick running (keyboard backend: " + InputHelper.Backend + ").");
            }

            gate.Tick();
            bridge.Tick();
            capture.Tick();
            cameraSync.Tick();
            router.Tick();

            if (guestLink != null)
            {
                TickGuestLink();
            }

            if (InputHelper.GetKeyDown(KeyCode.F6))
            {
                cameraSync.FollowEnabled = !cameraSync.FollowEnabled;
                config.FollowEnabled.Value = cameraSync.FollowEnabled;
                Logger.LogInfo("[MinecraftInRepo] Camera follow: " + (cameraSync.FollowEnabled ? "on" : "off"));
            }
            else if (InputHelper.GetKeyDown(KeyCode.F7))
            {
                Logger.LogInfo("[MinecraftInRepo] Calibrating worlds... stand where the two worlds should line up.");
                cameraSync.RequestCalibration();
            }
            else if (InputHelper.GetKeyDown(KeyCode.F8))
            {
                overlay.CycleMode();
            }
            else if (InputHelper.GetKeyDown(KeyCode.F9))
            {
                router.Enabled = !router.Enabled;
                config.BlastEnabled.Value = router.Enabled;
                Logger.LogInfo("[MinecraftInRepo] TNT damage in R.E.P.O.: " + (router.Enabled ? "armed" : "disarmed"));
            }
        }

        /// <summary>
        /// Phase 1 of the host/guest redesign: keep the v2 link up, decide who owns
        /// the player, and publish the host state. Nothing moves yet - that is
        /// phases 2 and 3 (collision export, follower, camera, input).
        /// </summary>
        private void TickGuestLink()
        {
            guestLink.Tick();

            bool linkUp = guestLink.LinkUp;
            if (linkUp && !handoffRequested)
            {
                handoffRequested = true;
                teleportSeq++;
            }
            else if (!linkUp)
            {
                handoffRequested = false;
            }

            Camera camera = Camera.main;
            bool hasCharacter = gate.Allowed && camera != null;

            Vector3 feet = hasCharacter
                ? map.RepoToMc(camera.transform.position - new Vector3(0f, HostEyeToFeet, 0f))
                : Vector3.zero;

            ownership.BeginFrame();
            Owner owner = ownership.Decide(new OwnershipInputs
            {
                LinkUp = linkUp,
                HasCharacter = hasCharacter,
                Cutscene = false,          // phase 0: the cutscene list comes from docs/REPO-NOTES.md
                HostMenuOpen = false,      // phase 5
                TeleportPending = handoffRequested && guestLink.Guest.TeleportAck != teleportSeq,
                GuestDead = guestLink.Guest.Dead
            });

            if (ownership.Changed)
            {
                Logger.LogInfo(string.Format(
                    "[MinecraftInRepo] owner {0} -> {1} ({2})",
                    ownership.Previous, owner, ownership.Reason));
                if (ownership.ReleaseGuestKeys)
                {
                    guestLink.SendReleaseAll();
                }
            }

            hostState.Owner = owner;
            hostState.TeleportSeq = teleportSeq;
            hostState.Loading = ownership.SendLoadingFlag;
            hostState.MenuOpen = ownership.SendMenuOpenFlag;
            hostState.ViewportWidth = Screen.width;
            hostState.ViewportHeight = Screen.height;
            hostState.X = feet.x;
            hostState.Y = feet.y;
            hostState.Z = feet.z;
            if (hasCharacter)
            {
                Vector3 forward = camera.transform.forward;
                hostState.Yaw = CoordinateMap.UnityYaw(forward);
                hostState.Pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            }
            guestLink.Publish(hostState);

            if (Time.unscaledTime >= nextGuestSummary)
            {
                nextGuestSummary = Time.unscaledTime + 1f;
                GuestState guest = guestLink.Guest;
                Logger.LogInfo(string.Format(
                    "[MinecraftInRepo] hg: owner={0} link={1} age={2}ms guest=({3:0.00},{4:0.00},{5:0.00}) " +
                    "hp={6:0.0} ack={7}/{8} frames={9}",
                    owner, linkUp ? "up" : "down", guestLink.GuestAgeMs,
                    guest.X, guest.Y, guest.Z, guest.Health,
                    guest.TeleportAck, teleportSeq, guestLink.HostSeq));
            }
        }

        /// <summary>Drawn from the plugin itself so the overlay cannot be lost
        /// together with a component Unity decided not to tick.</summary>
        private void OnGUI()
        {
            DrawFromDriver();
        }

        /// <summary>
        /// A short-lived background heartbeat. It runs outside Unity's frame loop,
        /// so it reports whether the plugin is alive even when Update/OnGUI are
        /// not being called - which is exactly the failure we are hunting.
        /// </summary>
        private void StartHeartbeat()
        {
            Thread thread = new Thread(() =>
            {
                for (int i = 1; i <= 12; i++)
                {
                    Thread.Sleep(5000);
                    Logger.LogInfo(string.Format(
                        "[MinecraftInRepo] heartbeat {0}s: tick-ran={1} ongui-ran={2} connected={3} pump={4} driver={5}",
                        i * 5, loggedFirstUpdate, overlay != null && overlay.GuiRan,
                        bridge != null && bridge.Connected, loggedPump, driverRan));
                }
            });
            thread.IsBackground = true;
            thread.Name = "MinecraftInRepo-Heartbeat";
            thread.Start();
        }
    }
}
