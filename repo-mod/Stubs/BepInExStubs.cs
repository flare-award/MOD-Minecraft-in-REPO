// ---------------------------------------------------------------------------
// Hand-written stub of the BepInEx 5.4 surface used by MinecraftInRepo.
// Only used when compiling without the game / without the BepInEx NuGet feed.
// ---------------------------------------------------------------------------

using System;
using UnityEngine;

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public void LogDebug(object data) { }
        public void LogInfo(object data) { }
        public void LogMessage(object data) { }
        public void LogWarning(object data) { }
        public void LogError(object data) { }
        public void LogFatal(object data) { }
    }
}

namespace BepInEx.Configuration
{
    public class ConfigEntry<T>
    {
        public T Value { get; set; }
    }

    public class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue)
        {
            return new ConfigEntry<T> { Value = defaultValue };
        }

        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
        {
            return new ConfigEntry<T> { Value = defaultValue };
        }
    }
}

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BepInPlugin : Attribute
    {
        public BepInPlugin(string GUID, string Name, string Version) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class BepInDependency : Attribute
    {
        public BepInDependency(string DependencyGUID) { }
    }

    public class PluginInfo
    {
        public string GUID => "stub";
    }

    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        protected BaseUnityPlugin()
        {
            Logger = new Logging.ManualLogSource();
            Config = new Configuration.ConfigFile();
        }

        public Logging.ManualLogSource Logger { get; private set; }
        public Configuration.ConfigFile Config { get; private set; }
        public PluginInfo Info { get; private set; }
    }
}
