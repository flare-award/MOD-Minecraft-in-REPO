// All user-facing settings, backed by the BepInEx config file
// (BepInEx/config/MinecraftInRepo.cfg). Calibration (F7) writes back into
// these entries, so BepInEx persists the alignment automatically.

using BepInEx.Configuration;
using UnityEngine;

namespace MinecraftInRepo
{
    public sealed class ModConfig
    {
        // [Net]
        public ConfigEntry<int> Port;
        public ConfigEntry<float> ConnectInterval;
        public ConfigEntry<int> SendRateHz;
        public ConfigEntry<float> PingSeconds;

        // [Overlay]
        public ConfigEntry<string> OverlayMode;   // Off | FullScreen | PiP
        public ConfigEntry<float> OverlayAlpha;
        public ConfigEntry<int> CaptureFps;
        public ConfigEntry<string> WindowTitleContains;
        public ConfigEntry<bool> ShowStatus;

        // [Camera]
        public ConfigEntry<bool> FollowEnabled;
        public ConfigEntry<float> AlignmentYawDeg;

        // [Map]
        public ConfigEntry<float> McOriginX;
        public ConfigEntry<float> McOriginY;
        public ConfigEntry<float> McOriginZ;
        public ConfigEntry<float> RepoAnchorX;
        public ConfigEntry<float> RepoAnchorY;
        public ConfigEntry<float> RepoAnchorZ;
        public ConfigEntry<float> Scale;

        // [Blast]
        public ConfigEntry<bool> BlastEnabled;
        public ConfigEntry<float> RadiusPerPower;
        public ConfigEntry<int> MaxEnemyDamage;
        public ConfigEntry<int> MaxPlayerDamage;
        public ConfigEntry<float> PlayerKnockback;
        public ConfigEntry<float> EnemyKnockback;
        public ConfigEntry<float> ItemForce;
        public ConfigEntry<bool> FriendlyFire;
        public ConfigEntry<bool> DestroyValuables;
        public ConfigEntry<float> DestroyThreshold;
        public ConfigEntry<bool> StunEnemies;

        public ModConfig(ConfigFile config)
        {
            Port = config.Bind("Net", "Port", 47621,
                "TCP port of the Minecraft bridge (must match mc-mod config/mcrepo-bridge.json).");
            ConnectInterval = config.Bind("Net", "ConnectInterval", 2f,
                "Seconds between connection attempts while Minecraft is not running.");
            SendRateHz = config.Bind("Net", "SendRateHz", 60,
                "How often the R.E.P.O. camera pose is sent to Minecraft (Hz).");
            PingSeconds = config.Bind("Net", "PingSeconds", 2f,
                "Keepalive interval to notice a dead Minecraft process.");

            OverlayMode = config.Bind("Overlay", "Mode", "FullScreen",
                "Where the live Minecraft window is shown inside R.E.P.O.: Off, FullScreen (ghost overlay) or PiP (corner picture-in-picture). F8 cycles modes.");
            OverlayAlpha = config.Bind("Overlay", "Alpha", 0.35f,
                "Opacity of the FullScreen ghost overlay (0..1).");
            CaptureFps = config.Bind("Overlay", "CaptureFps", 30,
                "Window-capture rate of the Minecraft window (frames per second).");
            WindowTitleContains = config.Bind("Overlay", "WindowTitleContains", "Minecraft",
                "Substring used to find the Minecraft window for capture.");
            ShowStatus = config.Bind("Overlay", "ShowStatus", true,
                "Show the small bridge status line.");

            FollowEnabled = config.Bind("Camera", "FollowEnabled", true,
                "Whether the Minecraft camera follows the R.E.P.O. camera. F6 toggles.");
            AlignmentYawDeg = config.Bind("Camera", "AlignmentYawDeg", 0f,
                "Yaw rotation between the R.E.P.O. level and the Minecraft world. Set automatically by F7 calibration.");

            McOriginX = config.Bind("Map", "McOriginX", 0f,
                "Minecraft coordinates that correspond to the R.E.P.O. anchor (set by F7 calibration).");
            McOriginY = config.Bind("Map", "McOriginY", 64f, "");
            McOriginZ = config.Bind("Map", "McOriginZ", 0f, "");
            RepoAnchorX = config.Bind("Map", "RepoAnchorX", 0f,
                "R.E.P.O. anchor point of the coordinate mapping (set by F7 calibration).");
            RepoAnchorY = config.Bind("Map", "RepoAnchorY", 0f, "");
            RepoAnchorZ = config.Bind("Map", "RepoAnchorZ", 0f, "");
            Scale = config.Bind("Map", "Scale", 1f,
                "R.E.P.O. meters per Minecraft block (1.0 = real scale).");

            BlastEnabled = config.Bind("Blast", "BlastEnabled", true,
                "Whether Minecraft explosions damage R.E.P.O. entities. F9 toggles.");
            RadiusPerPower = config.Bind("Blast", "RadiusPerPower", 1.5f,
                "Blast radius in R.E.P.O. meters = TNT power x this value (TNT power is 4).");
            MaxEnemyDamage = config.Bind("Blast", "MaxEnemyDamage", 90,
                "Point-blank explosion damage dealt to enemies.");
            MaxPlayerDamage = config.Bind("Blast", "MaxPlayerDamage", 45,
                "Point-blank explosion damage dealt to players.");
            PlayerKnockback = config.Bind("Blast", "PlayerKnockback", 6f,
                "Impulse applied to players caught in a blast.");
            EnemyKnockback = config.Bind("Blast", "EnemyKnockback", 5f,
                "Impulse applied to enemies with a physics body.");
            ItemForce = config.Bind("Blast", "ItemForce", 14f,
                "Explosion force applied to items and valuables (scaled by TNT power).");
            FriendlyFire = config.Bind("Blast", "FriendlyFire", true,
                "Whether your own TNT can hurt your own crew (it can still fling you).");
            DestroyValuables = config.Bind("Blast", "DestroyValuables", true,
                "Strong blasts shatter items/valuables through the game's own destruction path.");
            DestroyThreshold = config.Bind("Blast", "DestroyThreshold", 0.55f,
                "Minimum blast falloff (0..1) required to shatter an item/valuable.");
            StunEnemies = config.Bind("Blast", "StunEnemies", true,
                "Briefly stun enemies caught in a blast.");
        }

        public Vector3 OriginMc => new Vector3(McOriginX.Value, McOriginY.Value, McOriginZ.Value);

        public Vector3 AnchorRepo => new Vector3(RepoAnchorX.Value, RepoAnchorY.Value, RepoAnchorZ.Value);
    }
}
