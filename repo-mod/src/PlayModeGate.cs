// Decides whether the mod is allowed to do anything right now.
//
// Supported mode: SOLO. R.E.P.O. singleplayer + Minecraft singleplayer, both on
// one machine, talking over loopback. That is the whole point of the project,
// so the default configuration (General.SinglePlayerOnly = true) makes the mod
// go completely idle the moment it detects a multiplayer session:
//
//   * no camera streaming (Minecraft is not driven),
//   * no window capture / overlay,
//   * no explosion routing (no damage, no knockback, no shattering).
//
// That way an online session stays vanilla-clean for everyone else instead of
// half-working, and there is nothing that can desync another player's game.
// Set General.SinglePlayerOnly = false if you deliberately want to run it in
// co-op (host only - see README "Multiplayer notes").

using BepInEx.Logging;
using UnityEngine;

namespace MinecraftInRepo
{
    public sealed class PlayModeGate : MonoBehaviour
    {
        private const float CheckInterval = 1f;

        private ModConfig config;
        private ManualLogSource log;

        private float nextCheck;
        private PlayMode mode = PlayMode.Unknown;
        private bool loggedBlocked;

        /// <summary>True when the mod may stream the camera, draw and deal damage.</summary>
        public bool Allowed => !Blocked;

        /// <summary>True when we are in multiplayer and the user asked for solo-only.</summary>
        public bool Blocked => config.SinglePlayerOnly.Value && mode == PlayMode.Multiplayer;

        public bool IsMultiplayer => mode == PlayMode.Multiplayer;

        public string Status
        {
            get
            {
                if (Blocked)
                {
                    return "solo-only: multiplayer session detected - mod idle";
                }
                if (config.SinglePlayerOnly.Value && mode == PlayMode.Unknown)
                {
                    return "solo-only: waiting for a session";
                }
                return null;
            }
        }

        public void Init(ModConfig modConfig, ManualLogSource logger)
        {
            config = modConfig;
            log = logger;
        }

        /// <summary>Called from the plugin's own Update (see MinecraftInRepoPlugin).</summary>
        public void Tick()
        {
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }
            nextCheck = Time.unscaledTime + CheckInterval;

            PlayMode detected = Detect();
            if (detected == PlayMode.Unknown)
            {
                // Menu / loading / Photon not up yet: keep the last known state so
                // we do not flap between modes while a level is being generated.
                return;
            }

            if (detected != mode)
            {
                mode = detected;
                loggedBlocked = false;
                log.LogInfo("[MinecraftInRepo] Session mode: " +
                    (mode == PlayMode.Multiplayer ? "multiplayer" : "singleplayer"));
            }

            if (Blocked && !loggedBlocked)
            {
                loggedBlocked = true;
                log.LogWarning("[MinecraftInRepo] Multiplayer session detected and SinglePlayerOnly=true: " +
                    "camera sync, overlay and TNT damage are disabled until you play solo again " +
                    "(set General.SinglePlayerOnly=false to override).");
            }
        }

        private static PlayMode Detect()
        {
            try
            {
                return SemiFunc.IsMultiplayer() ? PlayMode.Multiplayer : PlayMode.Singleplayer;
            }
            catch
            {
                // Called too early (main menu) or the API moved: stay neutral.
                return PlayMode.Unknown;
            }
        }

        private enum PlayMode
        {
            Unknown,
            Singleplayer,
            Multiplayer
        }
    }
}
