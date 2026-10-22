using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MapGenerator
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.aedin.mapgenerator";
        public const string PluginName = "Map Generator";
        public const string PluginVersion = "0.1.6";

        internal static ManualLogSource Log { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> MenuHotkey { get; private set; }

        private void Awake()
        {
            Log = Logger;
            MenuHotkey = Config.Bind("General", "MenuHotkey", new KeyboardShortcut(KeyCode.F9),
                "Open the one-click map generator.");

            try
            {
                new Harmony(PluginGuid).PatchAll(typeof(Plugin).Assembly);
            }
            catch (System.Exception e)
            {
                Log.LogError($"Map Generator patches failed: {e}");
            }

            var go = new GameObject("MapGenerator");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<GeneratorWindow>();
            go.AddComponent<AreaPicker>();
            go.AddComponent<FogOverlay>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded (menu={MenuHotkey.Value})");
        }
    }
}
