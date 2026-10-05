// Keyboard input that works with BOTH Unity input backends.
//
// R.E.P.O. (Unity 2022) runs with the new Input System, and when a project is
// set to "Input System Package (New)" only, the legacy UnityEngine.Input API is
// never fed - a plugin that only calls Input.GetKeyDown() silently never sees a
// keypress. So we probe for UnityEngine.InputSystem.Keyboard at runtime (via
// reflection, so the plugin still compiles without the package) and fall back
// to the legacy API when it is not there.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MinecraftInRepo
{
    internal static class InputHelper
    {
        private static readonly Dictionary<KeyCode, string> KeyControlNames = new Dictionary<KeyCode, string>
        {
            { KeyCode.F1, "f1Key" },
            { KeyCode.F2, "f2Key" },
            { KeyCode.F3, "f3Key" },
            { KeyCode.F4, "f4Key" },
            { KeyCode.F5, "f5Key" },
            { KeyCode.F6, "f6Key" },
            { KeyCode.F7, "f7Key" },
            { KeyCode.F8, "f8Key" },
            { KeyCode.F9, "f9Key" },
            { KeyCode.F10, "f10Key" },
            { KeyCode.F11, "f11Key" },
            { KeyCode.F12, "f12Key" }
        };

        private static bool probed;
        private static bool useInputSystem;
        private static PropertyInfo keyboardCurrent;
        private static PropertyInfo wasPressedThisFrame;
        private static readonly Dictionary<KeyCode, PropertyInfo> keyControls = new Dictionary<KeyCode, PropertyInfo>();

        /// <summary>True once on the frame the key went down, in either input backend.</summary>
        public static bool GetKeyDown(KeyCode code)
        {
            if (!probed)
            {
                Probe();
            }

            if (useInputSystem)
            {
                try
                {
                    object keyboard = keyboardCurrent != null ? keyboardCurrent.GetValue(null) : null;
                    if (keyboard != null && wasPressedThisFrame != null &&
                        keyControls.TryGetValue(code, out PropertyInfo keyProperty) && keyProperty != null)
                    {
                        object control = keyProperty.GetValue(keyboard);
                        if (control != null)
                        {
                            return (bool)wasPressedThisFrame.GetValue(control);
                        }
                    }
                }
                catch
                {
                    // fall through to the legacy API
                }
            }

            try
            {
                return Input.GetKeyDown(code);
            }
            catch
            {
                // legacy input is disabled in this project
                return false;
            }
        }

        /// <summary>Which backend is actually being used (for the log / status line).</summary>
        public static string Backend
        {
            get
            {
                if (!probed)
                {
                    Probe();
                }
                return useInputSystem ? "new Input System" : "legacy Input";
            }
        }

        private static void Probe()
        {
            probed = true;
            try
            {
                Type keyboardType = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    keyboardType = assembly.GetType("UnityEngine.InputSystem.Keyboard");
                    if (keyboardType != null)
                    {
                        break;
                    }
                }
                if (keyboardType == null)
                {
                    return;
                }

                keyboardCurrent = keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                foreach (KeyValuePair<KeyCode, string> pair in KeyControlNames)
                {
                    PropertyInfo property = keyboardType.GetProperty(pair.Value, BindingFlags.Public | BindingFlags.Instance);
                    if (property == null)
                    {
                        continue;
                    }
                    keyControls[pair.Key] = property;
                    if (wasPressedThisFrame == null)
                    {
                        wasPressedThisFrame = property.PropertyType.GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);
                    }
                }
                useInputSystem = keyboardCurrent != null && wasPressedThisFrame != null && keyControls.Count > 0;
            }
            catch
            {
                useInputSystem = false;
            }
        }
    }
}
