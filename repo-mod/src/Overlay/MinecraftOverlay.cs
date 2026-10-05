// Draws the live Minecraft window feed inside R.E.P.O. and handles the F8 mode
// cycle. Also draws the blast flash and the small status line.

using BepInEx.Logging;
using MinecraftInRepo.Blast;
using MinecraftInRepo.Capture;
using MinecraftInRepo.Net;
using MinecraftInRepo.Sync;
using UnityEngine;

namespace MinecraftInRepo.Overlay
{
    public sealed class MinecraftOverlay : MonoBehaviour
    {
        private ModConfig config;
        private ManualLogSource log;
        private MinecraftCapture capture;
        private BridgeClient bridge;
        private ExplosionRouter router;
        private CameraSync cameraSync;
        private PlayModeGate gate;

        private GUIStyle statusStyle;
        private GUIStyle bannerStyle;
        private Texture2D statusBackground;
        private Texture2D bannerBackground;
        private int modeIndex;
        private float bannerUntil;
        private bool loggedFirstGui;
        private static readonly string[] Modes = { "FullScreen", "PiP", "Off" };

        public void Init(ModConfig modConfig, MinecraftCapture minecraftCapture, BridgeClient bridgeClient,
            ExplosionRouter explosionRouter, CameraSync sync, PlayModeGate playModeGate, ManualLogSource logger)
        {
            config = modConfig;
            capture = minecraftCapture;
            bridge = bridgeClient;
            router = explosionRouter;
            cameraSync = sync;
            gate = playModeGate;
            log = logger;
            modeIndex = Mathf.Max(0, System.Array.IndexOf(Modes, config.OverlayMode.Value));

            // A short banner right after load: without it the only proof that the
            // mod is alive is a one-line status at the bottom edge of the screen.
            bannerUntil = Time.unscaledTime + 14f;
        }

        /// <summary>True once Draw() has actually been called (diagnostics).</summary>
        public bool GuiRan => loggedFirstGui;

        public void CycleMode()
        {
            modeIndex = (modeIndex + 1) % Modes.Length;
            config.OverlayMode.Value = Modes[modeIndex];
            log.LogInfo("[MinecraftInRepo] Overlay mode: " + Modes[modeIndex]);
        }

        /// <summary>Called from the plugin's own OnGUI (see MinecraftInRepoPlugin).</summary>
        public void Draw()
        {
            if (!loggedFirstGui)
            {
                loggedFirstGui = true;
                log.LogInfo("[MinecraftInRepo] OnGUI running - the overlay/status line is being drawn.");
            }

            GUI.depth = -10000;

            string mode = Modes[modeIndex];
            bool idle = !gate.Allowed;
            Texture feed = idle ? null : capture.CurrentTexture;

            if (mode != "Off" && feed != null)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, mode == "FullScreen" ? Mathf.Clamp01(config.OverlayAlpha.Value) : 1f);
                if (mode == "FullScreen")
                {
                    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), feed);
                }
                else
                {
                    GUI.DrawTexture(PipRect(feed.width, feed.height), feed);
                }
                GUI.color = previous;
            }

            // Blast flash feedback.
            float sinceBoom = Time.time - router.LastBoomTime;
            if (sinceBoom >= 0f && sinceBoom < 0.3f && mode != "Off")
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.9f, 0.7f, (0.3f - sinceBoom) / 0.3f * 0.45f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), WhitePixel);
                GUI.color = previous;
            }

            if (Time.unscaledTime < bannerUntil)
            {
                DrawBanner();
            }

            if (config.ShowStatus.Value)
            {
                EnsureStatusStyle();

                string link = bridge.Connected ? "[Minecraft] LINKED" : "[Minecraft] not connected";
                string status = link
                    + (cameraSync.FollowEnabled ? ", camera following" : ", camera paused (F6)")
                    + " | overlay " + Modes[modeIndex] + " (F8)"
                    + (router.Enabled ? " | TNT live (F9)" : " | TNT disabled (F9)");
                if (capture.HasWindow)
                {
                    status += " | capturing \"" + capture.WindowTitle + "\"";
                }
                if (!bridge.Connected)
                {
                    status += "  <- start Minecraft (Fabric 1.21.1) with mcrepo and load a world";
                }
                string gateStatus = gate.Status;
                if (gateStatus != null)
                {
                    status += " | " + gateStatus;
                }

                float height = 24f;
                Rect rect = new Rect(8f, Screen.height - height - 8f, Mathf.Min(Screen.width - 16f, 1200f), height);
                GUI.Box(rect, GUIContent.none, statusStyle);
                GUI.Label(rect, status, statusStyle);
            }
        }

        /// <summary>Big "the mod is alive" banner for the first seconds after load.</summary>
        private void DrawBanner()
        {
            EnsureBannerStyle();
            float width = Mathf.Min(Screen.width - 40f, 720f);
            float height = 92f;
            Rect rect = new Rect((Screen.width - width) * 0.5f, 24f, width, height);
            GUI.Box(rect, GUIContent.none, bannerStyle);
            GUI.Label(rect,
                "Minecraft in R.E.P.O. v" + MinecraftInRepoPlugin.PluginVersion + " is running\n" +
                "F6 camera follow   F7 calibrate   F8 overlay (now: " + Modes[modeIndex] + ")   F9 TNT damage\n" +
                (bridge.Connected ? "Minecraft: LINKED" : "Minecraft: not connected (start the Fabric 1.21.1 game)"),
                bannerStyle);
        }

        private Rect PipRect(int feedWidth, int feedHeight)
        {
            float w = Screen.width * 0.28f;
            float h = feedHeight > 0 ? w * feedHeight / feedWidth : w * 9f / 16f;
            float margin = 12f;
            return new Rect(Screen.width - w - margin, margin, w, h);
        }

        private Texture2D whitePixel;

        private Texture2D WhitePixel
        {
            get
            {
                if (whitePixel == null)
                {
                    whitePixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                }
                return whitePixel;
            }
        }

        private void EnsureStatusStyle()
        {
            if (statusStyle != null)
            {
                return;
            }
            statusBackground = SolidTexture(new Color(0f, 0f, 0f, 0.55f));
            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 2, 2)
            };
            statusStyle.normal.background = statusBackground;
            statusStyle.normal.textColor = new Color(0.85f, 1f, 0.85f, 1f);
        }

        private void EnsureBannerStyle()
        {
            if (bannerStyle != null)
            {
                return;
            }
            bannerBackground = SolidTexture(new Color(0f, 0f, 0f, 0.75f));
            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 8, 8)
            };
            bannerStyle.normal.background = bannerBackground;
            bannerStyle.normal.textColor = new Color(1f, 1f, 1f, 1f);
        }

        private static Texture2D SolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }
    }
}
