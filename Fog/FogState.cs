using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using Newtonsoft.Json;

namespace MapGenerator
{
    internal sealed class FogCell
    {
        public int X;
        public int Y;
    }

    internal sealed class FogState
    {
        public string MapKey;
        public bool Enabled;
        public bool AnyTokenClears;
        public int Radius = 6;
        public int Width;
        public int Height;
        public List<FogCell> Revealed = new List<FogCell>();
        public List<string> PlayerInstanceIds = new List<string>();
        public List<string> PlayerBlockIds = new List<string>();

        public static string DirectoryPath => Path.Combine(Paths.ConfigPath, "MapGenerator", "fog");

        public static string FileFor(string mapKey)
        {
            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(mapKey ?? ""));
                var hex = BitConverter.ToString(hash).Replace("-", "");
                return Path.Combine(DirectoryPath, hex.Substring(0, 16) + ".json");
            }
        }

        public static FogState Load(string mapKey)
        {
            if (string.IsNullOrEmpty(mapKey))
                return null;
            var path = FileFor(mapKey);
            if (!File.Exists(path))
                return null;
            try
            {
                var state = JsonConvert.DeserializeObject<FogState>(File.ReadAllText(path));
                if (state == null || state.MapKey != mapKey)
                    return null;
                state.Revealed = state.Revealed ?? new List<FogCell>();
                state.PlayerInstanceIds = state.PlayerInstanceIds ?? new List<string>();
                state.PlayerBlockIds = state.PlayerBlockIds ?? new List<string>();
                return state;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read fog for this map: {e.Message}");
                return null;
            }
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(MapKey))
                return;
            Directory.CreateDirectory(DirectoryPath);
            var path = FileFor(MapKey);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(this, Formatting.Indented));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
        }

        public static void Delete(string mapKey)
        {
            if (string.IsNullOrEmpty(mapKey))
                return;
            var path = FileFor(mapKey);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
