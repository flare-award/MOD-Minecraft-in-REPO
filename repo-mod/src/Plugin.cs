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

        private void Awake()
        {
            Instance = this;
            config = new ModConfig(Config);
            map = new CoordinateMap(config);

            GameObject host = new GameObject("MinecraftInRepo");
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

            Logger.LogInfo(string.Format(
                "[MinecraftInRepo] {0} v{1} loaded. Waiting for Minecraft (bridge port {2}). " +
                "Hotkeys: F6 camera follow, F7 calibrate, F8 overlay, F9 TNT damage. " +
                (config.SinglePlayerOnly.Value
                    ? "Mode: solo only (the mod idles in multiplayer sessions)."
                    : "Mode: solo + co-op (SinglePlayerOnly=false)."),
                PluginName, PluginVersion, config.Port.Value));
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F6))
            {
                cameraSync.FollowEnabled = !cameraSync.FollowEnabled;
                config.FollowEnabled.Value = cameraSync.FollowEnabled;
                Logger.LogInfo("[MinecraftInRepo] Camera follow: " + (cameraSync.FollowEnabled ? "on" : "off"));
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.F7))
            {
                Logger.LogInfo("[MinecraftInRepo] Calibrating worlds... stand where the two worlds should line up.");
                cameraSync.RequestCalibration();
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.F8))
            {
                overlay.CycleMode();
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.F9))
            {
                router.Enabled = !router.Enabled;
                config.BlastEnabled.Value = router.Enabled;
                Logger.LogInfo("[MinecraftInRepo] TNT damage in R.E.P.O.: " + (router.Enabled ? "armed" : "disarmed"));
            }
        }
    }
}
