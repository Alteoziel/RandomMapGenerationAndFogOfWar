using DungeonAlchemist.Data;
using DungeonAlchemist.MapEditing;
using HarmonyLib;

namespace MapGenerator
{
    [HarmonyPatch(typeof(MapEditor), nameof(MapEditor.MoveObject))]
    internal static class MoveRevealPatch
    {
        private static void Postfix(MoveObjectOperation __result)
        {
            try { FogOverlay.OnMoved(__result); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"Fog update failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(MapEditor), nameof(MapEditor.MoveObjectAndIgnoreCollisions))]
    internal static class MoveIgnoreRevealPatch
    {
        private static void Postfix(MoveObjectOperation __result)
        {
            try { FogOverlay.OnMoved(__result); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"Fog update failed: {e.Message}"); }
        }
    }
}
