// Captures the real Minecraft window (found by its title) into a Texture2D.
//
// Primary path: PrintWindow with PW_RENDERFULLCONTENT, which asks the Desktop
// Window Manager to render the window into our DC even when it is occluded.
// Fallback path: BitBlt the window's on-screen rectangle from the screen DC
// (works whenever the window is actually visible).

using System;
using System.Collections;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using UnityEngine;

namespace MinecraftInRepo.Capture
{
    public sealed class MinecraftCapture : MonoBehaviour
    {
        private ModConfig config;
        private PlayModeGate gate;
        private ManualLogSource log;

        private IntPtr hwnd = IntPtr.Zero;
        private Texture2D texture;
        private int textureWidth;
        private int textureHeight;
        private float nextWindowScan;
        private float captureAccumulator;

        public Texture CurrentTexture { get; private set; }
        public bool HasWindow => hwnd != IntPtr.Zero;
        public string WindowTitle { get; private set; }

        public void Init(ModConfig modConfig, PlayModeGate playModeGate, ManualLogSource logger)
        {
            config = modConfig;
            gate = playModeGate;
            log = logger;
        }

        /// <summary>Called once per frame from the plugin's tick (no coroutines:
        /// MonoBehaviour messages are not delivered to plugin components in
        /// R.E.P.O., so a coroutine would never advance).</summary>
        public void Tick()
        {
            float fps = Mathf.Clamp(config.CaptureFps.Value, 1, 120);
            captureAccumulator += Time.unscaledDeltaTime;
            if (captureAccumulator < 1f / fps)
            {
                return;
            }
            captureAccumulator = 0f;

            if (gate != null && !gate.Allowed)
            {
                // Idle in multiplayer: do not burn CPU on window capture.
                CurrentTexture = null;
                return;
            }

            if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd))
            {
                if (Time.unscaledTime >= nextWindowScan)
                {
                    nextWindowScan = Time.unscaledTime + 3f;
                    ScanForWindow();
                }
                CurrentTexture = null;
                return;
            }

            try
            {
                CaptureFrame();
            }
            catch (Exception e)
            {
                log.LogWarning("[MinecraftInRepo] Window capture failed: " + e.Message);
                CurrentTexture = null;
            }
        }

        private void ScanForWindow()
        {
            string wanted = config.WindowTitleContains.Value ?? "Minecraft";
            IntPtr found = IntPtr.Zero;
            string foundTitle = null;
            Win32.EnumWindows((hWnd, lParam) =>
            {
                if (!Win32.IsWindowVisible(hWnd))
                {
                    return true;
                }
                string title = Win32.GetWindowTitle(hWnd);
                if (title.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = hWnd;
                    foundTitle = title;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (found != IntPtr.Zero)
            {
                hwnd = found;
                WindowTitle = foundTitle;
                log.LogInfo("[MinecraftInRepo] Capturing Minecraft window: \"" + foundTitle + "\"");
            }
            else if (hwnd != IntPtr.Zero)
            {
                hwnd = IntPtr.Zero;
                log.LogInfo("[MinecraftInRepo] Minecraft window lost; scanning again.");
            }
        }

        private void CaptureFrame()
        {
            Win32.RECT client;
            if (!Win32.GetClientRect(hwnd, out client))
            {
                hwnd = IntPtr.Zero;
                return;
            }
            int width = client.right - client.left;
            int height = client.bottom - client.top;
            if (width < 16 || height < 16)
            {
                return; // minimized or still starting up
            }

            IntPtr screenDc = Win32.GetDC(IntPtr.Zero);
            IntPtr memDc = Win32.CreateCompatibleDC(screenDc);
            IntPtr bitmap = Win32.CreateCompatibleBitmap(screenDc, width, height);
            IntPtr oldBitmap = Win32.SelectObject(memDc, bitmap);
            try
            {
                bool rendered = Win32.PrintWindow(hwnd, memDc, Win32.PW_RENDERFULLCONTENT);
                if (!rendered)
                {
                    // Fall back to grabbing the visible window rect from the screen.
                    Win32.RECT window;
                    if (Win32.GetWindowRect(hwnd, out window))
                    {
                        int w = window.right - window.left;
                        int h = window.bottom - window.top;
                        if (w == width && h == height)
                        {
                            rendered = Win32.BitBlt(memDc, 0, 0, width, height, screenDc, window.left, window.top, Win32.SRCCOPY);
                        }
                    }
                }
                if (!rendered)
                {
                    return;
                }

                if (texture == null || textureWidth != width || textureHeight != height)
                {
                    if (texture != null)
                    {
                        UnityEngine.Object.Destroy(texture);
                    }
                    texture = new Texture2D(width, height, TextureFormat.BGRA32, false);
                    textureWidth = width;
                    textureHeight = height;
                }

                Win32.BITMAPINFO bmi = new Win32.BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(Win32.BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height; // top-down rows
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = Win32.BI_RGB;

                int byteCount = width * height * 4;
                IntPtr bits = Marshal.AllocHGlobal(byteCount);
                try
                {
                    int lines = Win32.GetDIBits(memDc, bitmap, 0, (uint)height, bits, ref bmi, Win32.DIB_RGB_COLORS);
                    if (lines != height)
                    {
                        return;
                    }
                    byte[] pixels = new byte[byteCount];
                    Marshal.Copy(bits, pixels, 0, byteCount);
                    texture.LoadRawTextureData(pixels);
                    texture.Apply(false);
                    CurrentTexture = texture;
                }
                finally
                {
                    Marshal.FreeHGlobal(bits);
                }
            }
            finally
            {
                Win32.SelectObject(memDc, oldBitmap);
                Win32.DeleteObject(bitmap);
                Win32.DeleteDC(memDc);
                Win32.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }
}
