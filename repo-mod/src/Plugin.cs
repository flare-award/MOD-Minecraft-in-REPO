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
