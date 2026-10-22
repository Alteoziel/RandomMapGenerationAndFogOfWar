using System.Collections.Generic;

namespace MapGenerator
{
    internal sealed class BuildRequest
    {
        public int MapWidth = 30;
        public int MapHeight = 30;
        public int BuildingCount = 6;
        public int MinBuildingSize = 4;
        public int MaxBuildingSize = 8;
        public bool PickThemes;
        public readonly HashSet<string> ThemeIds = new HashSet<string>();
        public bool PickPlayers;
        public readonly HashSet<string> PlayerIds = new HashSet<string>();
        public int PlayerCount = 4;
        public bool PlayersInArea;
        public bool HasArea;
        public int AreaX;
        public int AreaY;
        public int AreaW;
        public int AreaH;
        public bool PickEnemies;
        public readonly HashSet<string> EnemyIds = new HashSet<string>();
        public int EnemyCount = 8;
        public bool Fog = true;
        public int VisionTiles = 6;
        public bool ForceWorkshop = true;
        public bool ClearRooms = true;
    }

    internal static class Session
    {
        public static readonly BuildRequest Request = new BuildRequest();
        public static bool WindowOpen;
        public static bool PickingArea;
        public static bool Busy;
        public static bool ConfirmClear;
        public static string Status = "Open a map, choose what you want, then generate.";
    }
}
