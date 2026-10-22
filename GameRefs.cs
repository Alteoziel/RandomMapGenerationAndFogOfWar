using DungeonAlchemist.Data;
using DungeonAlchemist.MapEditing;
using DungeonAlchemist.MapViewing;
using UnityEngine;

namespace MapGenerator
{
    internal static class GameRefs
    {
        public static MapEditor Editor => Object.FindObjectOfType<MapEditor>();

        public static MapManager Manager
        {
            get
            {
                var view = View;
                var shown = view != null ? view.Map : null;
                if (shown != null)
                {
                    var managers = Object.FindObjectsOfType<MapManager>();
                    for (int i = 0; i < managers.Length; i++)
                    {
                        if (managers[i] != null && managers[i].Map == shown)
                            return managers[i];
                    }
                }
                return Object.FindObjectOfType<MapManager>();
            }
        }

        public static Map Map
        {
            get
            {
                var view = View;
                if (view != null && view.Map != null)
                    return view.Map;
                var manager = Object.FindObjectOfType<MapManager>();
                return manager != null ? manager.Map : null;
            }
        }

        public static MapView View
        {
            get
            {
                var editor = Editor;
                return editor != null ? editor.GetMapView() : null;
            }
        }

        public static string MapKey
        {
            get
            {
                var manager = Manager;
                if (manager == null)
                    return null;
                var path = manager.GetSavePath();
                if (!string.IsNullOrEmpty(path))
                    return path;
                var name = manager.MapName;
                return string.IsNullOrEmpty(name) ? "unsaved" : "unsaved:" + name;
            }
        }
    }
}
