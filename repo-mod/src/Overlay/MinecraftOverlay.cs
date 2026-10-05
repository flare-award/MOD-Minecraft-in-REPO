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
        private Texture2D statusBackground;
        private int modeIndex;
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
        }

        public void CycleMode()
        {
            modeIndex = (modeIndex + 1) % Modes.Length;
            config.OverlayMode.Value = Modes[modeIndex];
            log.LogInfo("[MinecraftInRepo] Overlay mode: " + Modes[modeIndex]);
        }

        private void OnGUI()
        {
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

            if (config.ShowStatus.Value)
            {
                EnsureStatusStyle();
                string status = bridge.Connected
                    ? "[Minecraft] linked" + (cameraSync.FollowEnabled ? ", camera following" : ", camera paused (F6)")
                    : "[Minecraft] not connected (start Minecraft with the bridge mod)";
                if (capture.HasWindow)
                {
                    status += ", capturing \"" + capture.WindowTitle + "\"";
                }
                status += " | mode " + Modes[modeIndex] + " (F8)";
                status += router.Enabled ? ", TNT live (F9)" : ", TNT disabled (F9)";
                string gateStatus = gate.Status;
                if (gateStatus != null)
                {
                    status += " | " + gateStatus;
                }
                GUI.Label(new Rect(8f, Screen.height - 26f, Screen.width - 16f, 22f), status, statusStyle);
            }
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
            statusBackground = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            statusStyle = new GUIStyle
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft
            };
            statusStyle.normal.background = statusBackground;
            statusStyle.normal.textColor = new Color(0.9f, 1f, 0.9f, 1f);
        }
    }
}
