using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonAlchemist;
using DungeonAlchemist.Collisions;
using DungeonAlchemist.Config;
using DungeonAlchemist.Data;
using DungeonAlchemist.Database;
using DungeonAlchemist.MapEditing;
using DungeonAlchemist.RoomGeneration;
using UnityEngine;

namespace MapGenerator
{
    internal static class BuildRunner
    {
        public static IEnumerator Run(BuildRequest request)
        {
            if (Session.Busy)
                yield break;
            Session.Busy = true;
            Session.ConfirmClear = false;
            IDisposable workshop = null;
            var core = Generate(request, scope => workshop = scope);
            while (true)
            {
                object current = null;
                bool more;
                try
                {
                    more = core.MoveNext();
                    if (more)
                        current = core.Current;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Map generation failed: {e}");
                    Session.Status = "Generation failed. See the BepInEx log for the reason.";
                    break;
                }
                if (!more)
                    break;
                yield return current;
            }
            try { workshop?.Dispose(); }
            catch (Exception e) { Plugin.Log.LogWarning($"Could not restore Workshop settings: {e.Message}"); }
            Session.Busy = false;
        }

        private static IEnumerator Generate(BuildRequest request, Action<IDisposable> rememberWorkshop)
        {
                var editor = GameRefs.Editor;
                var map = GameRefs.Map;
                if (editor == null || map == null || map.database == null || map.surfaceTerrainBaseFoundation == null)
                {
                    Session.Status = "Open a map first.";
                    yield break;
                }
                if (request.PlayersInArea && !request.HasArea)
                {
                    Session.Status = "Choose an area on the map, or place player tokens anywhere.";
                    yield break;
                }

                int width = Mathf.Clamp(request.MapWidth, 8, 80);
                int height = Mathf.Clamp(request.MapHeight, 8, 80);
                int minSize = Mathf.Clamp(request.MinBuildingSize, 2, 20);
                int maxSize = Mathf.Clamp(Mathf.Max(request.MaxBuildingSize, minSize), minSize, 24);
                int buildings = Mathf.Clamp(request.BuildingCount, 1, 40);

                if (request.ForceWorkshop)
                {
                    var scope = WorkshopBridge.Force();
                    rememberWorkshop(scope);
                    if (scope == null)
                        Session.Status = "Workshop Procgen is not installed. Using the game's own assets.";
                }

                var view = GameRefs.View;
                Plugin.Log.LogInfo($"Generate started. Map {map.width}x{map.height}, view {(view != null ? view.Size.ToString() : "none")}, requested {width}x{height}, busy={editor.IsBusy()}.");
                var ready = WaitUntilReady(editor);
                while (ready.MoveNext())
                    yield return ready.Current;
                if (editor.IsBusy())
                {
                    Session.Status = "The map is still busy. Try again in a moment.";
                    Plugin.Log.LogWarning(Session.Status);
                    yield break;
                }

                editor.DisableAddRoomAnimation();
                map = GameRefs.Map ?? map;
                int extraX = Mathf.Max(0, width - map.width);
                int extraY = Mathf.Max(0, height - map.height);
                if (extraX > 0 || extraY > 0)
                {
                    Session.Status = "Resizing the map…";
                    Plugin.Log.LogInfo($"Resizing by {extraX} x {extraY}.");
                    editor.ResizeMap(new ResizeMapMargins(0, extraX, 0, extraY));
                    var resized = WaitUntilReady(editor);
                    while (resized.MoveNext())
                        yield return resized.Current;
                    map = GameRefs.Map ?? map;
                    Plugin.Log.LogInfo($"Resize finished. Map is {map.width}x{map.height}, busy={editor.IsBusy()}.");
                    if (editor.IsBusy())
                    {
                        Session.Status = "The map did not finish resizing.";
                        Plugin.Log.LogWarning(Session.Status);
                        yield break;
                    }
                }

                if (request.ClearRooms)
                {
                    Session.Status = "Removing existing rooms…";
                    var clearing = ClearNormalRooms(editor, map);
                    while (clearing.MoveNext())
                        yield return clearing.Current;
                    map = GameRefs.Map ?? map;
                }

                var foundation = map.surfaceTerrainBaseFoundation;
                var free = CollectFreeTiles(map, foundation, width, height);
                var rng = new System.Random();
                var rooms = Pack(free, buildings, minSize, maxSize, rng);
                var themes = ThemesFor(map.database, request);
                Plugin.Log.LogInfo($"Open tiles {free.Count}, buildings that fit {rooms.Count}, themes {themes.Count}.");
                if (rooms.Count == 0)
                {
                    Session.Status = "No open space fit a building. Clear the map, or use a larger size and smaller buildings.";
                    Plugin.Log.LogWarning(Session.Status);
                    yield break;
                }
                if (themes.Count == 0)
                {
                    Session.Status = "No building themes are available.";
                    Plugin.Log.LogWarning(Session.Status);
                    yield break;
                }

                var placedTiles = new List<List<Vector2i>>();
                int placed = 0;
                for (int i = 0; i < rooms.Count; i++)
                {
                    Session.Status = $"Generating building {i + 1} of {rooms.Count}…";
                    var roomReady = WaitUntilReady(editor);
                    while (roomReady.MoveNext())
                        yield return roomReady.Current;
                    if (editor.IsBusy())
                    {
                        Session.Status = $"Stopped after {placed} buildings. The map was still busy.";
                        Plugin.Log.LogWarning(Session.Status);
                        break;
                    }
                    var theme = themes[rng.Next(themes.Count)];
                    var config = PickRoom(theme, rng);
                    if (config == null)
                    {
                        Plugin.Log.LogWarning($"Theme '{theme.name}' has no regular room.");
                        continue;
                    }
                    var parts = rooms[i].Select(RoomInstancePartData.CreateTile).ToList();
                    var pending = editor.AddRoom(config, NewSettings(), new FoundationDataPartSet(map, foundation.data.id, parts));
                    Plugin.Log.LogInfo($"Queued '{config.name}' ({parts.Count} tiles) as {pending?.GetType().Name ?? "null"}.");
                    var applied = WaitApplied(editor, pending);
                    while (applied.MoveNext())
                        yield return applied.Current;
                    var operation = Operation(pending);
                    if (operation != null && !operation.success)
                    {
                        Plugin.Log.LogWarning($"'{config.name}' was rejected by the map.");
                        continue;
                    }
                    placed++;
                    placedTiles.Add(rooms[i]);
                    yield return null;
                }

                map = GameRefs.Map ?? map;
                foundation = map.surfaceTerrainBaseFoundation;
                var catalog = Catalog.Current;
                if (catalog == null)
                {
                    Catalog.Refresh(map.database);
                    catalog = Catalog.Current;
                }

                var playerInstances = new List<string>();
                int players = PlaceTokens(editor, map, foundation, request, catalog, rng, placedTiles, playerInstances, enemies: false);
                int enemies = PlaceTokens(editor, map, foundation, request, catalog, rng, placedTiles, playerInstances, enemies: true);

                if (request.Fog)
                {
                    FogOverlay.Cover(request.VisionTiles, playerInstances, PlayerBlocks(request));
                    Session.Status = $"Placed {placed} buildings, {players} player tokens, and {enemies} enemy tokens. Fog covers the ground until a player token is moved.";
                }
                else
                {
                    FogOverlay.ClearSaved();
                    Session.Status = $"Placed {placed} buildings, {players} player tokens, and {enemies} enemy tokens.";
                }
                if (placed < rooms.Count)
                    Session.Status += $" {rooms.Count - placed} buildings did not finish.";
        }

        private static List<string> PlayerBlocks(BuildRequest request)
        {
            var blocks = new List<string>();
            if (!request.PickPlayers || request.PlayerIds.Count == 0)
                return blocks;
            foreach (var id in request.PlayerIds)
            {
                if (request.PickEnemies && request.EnemyIds.Contains(id))
                    continue;
                blocks.Add(id);
            }
            return blocks;
        }

        private static int PlaceTokens(MapEditor editor, Map map, Foundation foundation, BuildRequest request, Catalog catalog,
            System.Random rng, List<List<Vector2i>> rooms, List<string> playerInstances, bool enemies)
        {
            int wanted = Mathf.Max(0, enemies ? request.EnemyCount : request.PlayerCount);
            if (wanted == 0 || catalog == null || catalog.Tokens.Count == 0)
                return 0;

            var spots = new List<Vector2>();
            if (enemies)
            {
                foreach (var room in rooms)
                {
                    foreach (var tile in room)
                    {
                        try
                        {
                            var center = foundation.FoundationToMapTile(tile).center;
                            spots.Add(center);
                        }
                        catch { /* skip a tile the foundation cannot describe */ }
                    }
                }
            }
            else if (request.PlayersInArea && request.HasArea)
            {
                for (int x = request.AreaX; x < request.AreaX + request.AreaW; x++)
                for (int y = request.AreaY; y < request.AreaY + request.AreaH; y++)
                    if (map.IsOnMap(new Vector2i(x, y)))
                        spots.Add(new Vector2(x + 0.5f, y + 0.5f));
            }
            else
            {
                int w = Mathf.Min(request.MapWidth, map.width);
                int h = Mathf.Min(request.MapHeight, map.height);
                for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    spots.Add(new Vector2(x + 0.5f, y + 0.5f));
            }

            Shuffle(spots, rng);
            var chosen = enemies ? request.EnemyIds : request.PlayerIds;
            bool specific = (enemies ? request.PickEnemies : request.PickPlayers) && chosen.Count > 0;
            var pool = specific ? catalog.Tokens.Where(token => chosen.Contains(token.Id)).ToList() : catalog.Tokens;
            if (pool.Count == 0)
                pool = catalog.Tokens;

            int placed = 0;
            int attempts = 0;
            while (placed < wanted && attempts < wanted * 12 && spots.Count > 0)
            {
                attempts++;
                int index = rng.Next(spots.Count);
                var spot = spots[index];
                spots.RemoveAt(index);
                var token = pool[rng.Next(pool.Count)];
                var config = map.database.BuildingBlockConfigs.GetOrNull(token.Id);
                if (config == null)
                    continue;
                if (!TryPlace(editor, map, foundation, config, spot, rng, out var instanceId))
                    continue;
                placed++;
                if (!enemies && !string.IsNullOrEmpty(instanceId))
                    playerInstances.Add(instanceId);
            }
            return placed;
        }

        private static bool TryPlace(MapEditor editor, Map map, Foundation foundation, BuildingBlockConfig config, Vector2 mapPos, System.Random rng, out string instanceId)
        {
            instanceId = null;
            try
            {
                float rotation = rng.Next(4) * 90f;
                var template = BuildingBlockInstanceDataTemplate.CreateNonVariantTemplate(BuildingBlockInstanceData.CreateObject(config, mapPos));
                var surface = Surface(map, foundation, mapPos);
                var first = editor.AddObject(surface, template, rotation, ClipToGrid.Default, false, false, true, true, 1f);
                if (first != null && first.result == PlaceObjectOperationResult.Success)
                {
                    instanceId = first.addedInstanceId;
                    return true;
                }
                template = BuildingBlockInstanceDataTemplate.CreateNonVariantTemplate(BuildingBlockInstanceData.CreateObject(config, mapPos));
                var second = editor.AddObject(surface, template, rotation, ClipToGrid.NeverClip, true, false, true, false, 1f);
                if (second != null && second.result == PlaceObjectOperationResult.Success)
                {
                    instanceId = second.addedInstanceId;
                    return true;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Token '{config.name}' was not placed: {e.Message}");
            }
            return false;
        }

        private static PlaceableObjectSurface Surface(Map map, Foundation foundation, Vector2 mapPos)
        {
            float height = 0f;
            try
            {
                height = map.GetPlacementHeight(foundation, foundation.MapToFoundation(mapPos), out _);
            }
            catch { /* the game will reject an unusable spot */ }
            return new PlaceableObjectSurface
            {
                mapPosition = mapPos,
                height = height,
                fixedHeight = true,
                normal = Vector3.up,
                mostUpVector = Vector3.up,
                depth = 1f,
            };
        }

        private static HashSet<Vector2i> CollectFreeTiles(Map map, Foundation foundation, int width, int height)
        {
            var free = new HashSet<Vector2i>();
            int maxX = Mathf.Min(width, map.width);
            int maxY = Mathf.Min(height, map.height);
            int inRoom = 0, invalid = 0, unreadable = 0;
            string firstProblem = null;
            for (int x = 0; x < maxX; x++)
            {
                for (int y = 0; y < maxY; y++)
                {
                    var mapPos = new Vector2(x + 0.5f, y + 0.5f);
                    try
                    {
                        if (foundation.IsInNormalRoom(mapPos))
                        {
                            inRoom++;
                            continue;
                        }
                        var tile = foundation.MapToFoundationTile(mapPos);
                        if (!foundation.IsValidTile(tile.tile))
                        {
                            invalid++;
                            continue;
                        }
                        free.Add(tile.tile);
                    }
                    catch (Exception e)
                    {
                        unreadable++;
                        firstProblem ??= e.Message;
                    }
                }
            }
            Plugin.Log.LogInfo($"Scanned {maxX}x{maxY}: free {free.Count}, already in a room {inRoom}, off the map {invalid}, unreadable {unreadable}.");
            if (firstProblem != null)
                Plugin.Log.LogWarning($"First tile that could not be read: {firstProblem}");
            return free;
        }

        private static List<List<Vector2i>> Pack(HashSet<Vector2i> free, int count, int min, int max, System.Random rng)
        {
            var starts = new List<Vector2i>(free);
            Shuffle(starts, rng);
            var blocked = new HashSet<Vector2i>();
            var rooms = new List<List<Vector2i>>();
            foreach (var origin in starts)
            {
                if (rooms.Count >= count)
                    break;
                if (blocked.Contains(origin))
                    continue;
                List<Vector2i> rect = null;
                for (int attempt = 0; attempt < 8 && rect == null; attempt++)
                    rect = TryRect(free, blocked, origin, rng.Next(min, max + 1), rng.Next(min, max + 1));
                if (rect == null)
                    continue;
                rooms.Add(rect);
                foreach (var tile in rect)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        blocked.Add(new Vector2i(tile.x + dx, tile.y + dy));
                }
            }
            return rooms;
        }

        private static List<Vector2i> TryRect(HashSet<Vector2i> free, HashSet<Vector2i> blocked, Vector2i origin, int width, int height)
        {
            var rect = new List<Vector2i>(width * height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var tile = new Vector2i(origin.x + x, origin.y + y);
                    if (!free.Contains(tile) || blocked.Contains(tile))
                        return null;
                    rect.Add(tile);
                }
            }
            return rect.Count > 0 ? rect : null;
        }

        private static IEnumerator ClearNormalRooms(MapEditor editor, Map map)
        {
            var skipped = new HashSet<string>();
            for (int n = 0; n < 300; n++)
            {
                RoomInstanceData room = null;
                foreach (var candidate in map.rooms.List())
                {
                    if (candidate == null || !map.IsNormalRoom(candidate) || !skipped.Add(candidate.id))
                        continue;
                    room = candidate;
                    break;
                }
                if (room == null)
                    yield break;
                var ready = WaitUntilReady(editor);
                while (ready.MoveNext())
                    yield return ready.Current;
                if (editor.IsBusy())
                    yield break;
                var pending = editor.DeleteRoom(room);
                var applied = WaitApplied(editor, pending);
                while (applied.MoveNext())
                    yield return applied.Current;
                map = GameRefs.Map ?? map;
            }
        }

        private static List<ThemeConfig> ThemesFor(DatabaseConnection db, BuildRequest request)
        {
            bool picked = request.PickThemes && request.ThemeIds.Count > 0;
            var themes = new List<ThemeConfig>();
            var fallback = new List<ThemeConfig>();
            foreach (var theme in db.ThemeConfigs.List() ?? new List<ThemeConfig>())
            {
                if (theme == null || !theme.visible || !theme.IsAvailable() || theme.roomConfigs == null)
                    continue;
                if (!theme.roomConfigs.Any(room => room != null && room.normalRoom))
                    continue;
                fallback.Add(theme);
                if (picked && !request.ThemeIds.Contains(theme.Id))
                    continue;
                if (!picked && !theme.allowInInitialRandomSelection)
                    continue;
                themes.Add(theme);
            }
            return themes.Count > 0 ? themes : fallback;
        }

        private static RoomConfig PickRoom(ThemeConfig theme, System.Random rng)
        {
            var rooms = theme.GetValidRoomConfigs(RoomInstanceType.Regular).Where(room => room != null && room.normalRoom).ToList();
            if (rooms.Count == 0)
                return null;
            return rooms[rng.Next(rooms.Count)];
        }

        private static AIConfig NewSettings()
        {
            return new AIConfig(true)
            {
                allowObjects = true,
                allowWalls = true,
                allowDoors = true,
                allowWindows = true,
                allowLights = true,
                allowRoofs = AllowRoofSetting.ForceToRoomDefault,
                refreshAllFloorTiles = true,
                roomInstanceType = RoomInstanceType.Regular,
            };
        }

        private static AddRoomOperation Operation(object pending)
        {
            return pending is PendingOperation<AddRoomOperation> operation ? operation.GetOperation() : null;
        }

        private static IEnumerator WaitApplied(MapEditor editor, object pending)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 180f)
            {
                if (IsApplied(pending))
                    yield break;
                if (!editor.IsBusy() && Time.realtimeSinceStartup - start > 2f)
                    yield break;
                yield return null;
            }
        }

        private static bool IsApplied(object pending)
        {
            if (pending is PendingOperation<AddRoomOperation> added)
                return added.IsDoneAndAppliedToMap();
            if (pending is PendingOperation<DeleteRoomOperation> deleted)
                return deleted.IsDoneAndAppliedToMap();
            return false;
        }

        private static IEnumerator WaitUntilReady(MapEditor editor)
        {
            float start = Time.realtimeSinceStartup;
            while (editor != null && editor.IsBusy() && Time.realtimeSinceStartup - start < 180f)
                yield return null;
        }

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }
    }
}
