using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGenerator
{
    internal sealed class GeneratorWindow : MonoBehaviour
    {
        private const int WindowId = 0x4D415047;

        private Rect _window = new Rect(28f, 28f, 560f, 760f);
        private Vector2 _scroll;
        private Vector2 _themeScroll;
        private Vector2 _playerScroll;
        private Vector2 _enemyScroll;
        private string _themeSearch = "";
        private string _playerSearch = "";
        private string _enemySearch = "";
        private bool _catalogReady;

        private void Update()
        {
            var hotkey = Plugin.MenuHotkey;
            if (hotkey != null && hotkey.Value.IsDown() && !Session.PickingArea)
                SetOpen(!Session.WindowOpen);
        }

        private void SetOpen(bool open)
        {
            Session.WindowOpen = open;
            Session.ConfirmClear = false;
            if (!open)
                return;
            int radius = FogOverlay.ActiveRadius;
            if (radius > 0)
                Session.Request.VisionTiles = Mathf.Clamp(radius, 1, 20);
            try
            {
                var map = GameRefs.Map;
                if (map != null)
                    Catalog.Refresh(map.database);
                _catalogReady = Catalog.Current != null;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not list themes and tokens: {e.Message}");
                Session.Status = "The asset list could not be read yet. Open a map and try again.";
            }
        }

        private void OnGUI()
        {
            if (!Session.WindowOpen || Session.PickingArea)
                return;
            UiSkin.Ensure();
            var previous = GUI.skin;
            GUI.skin = UiSkin.Skin;
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Map Generator");
            GUI.skin = previous;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                SetOpen(false);
                Event.current.Use();
            }
        }

        private void DrawWindow(int id)
        {
            var request = Session.Request;
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label(Session.Status, UiSkin.Body);
            GUILayout.Space(6f);

            Section("Buildings");
            Slider("Map width", ref request.MapWidth, 8, 80, "tiles");
            Slider("Map height", ref request.MapHeight, 8, 80, "tiles");
            Slider("Buildings", ref request.BuildingCount, 1, 40, "");
            Slider("Smallest building", ref request.MinBuildingSize, 2, 16, "tiles");
            Slider("Largest building", ref request.MaxBuildingSize, request.MinBuildingSize, 20, "tiles");
            GUILayout.Label("Each building is one generated room. Its floors, walls, doors, windows, and props come from the theme you pick. Leave the list on random if you want a mix.", UiSkin.MutedLabel);
            request.PickThemes = GUILayout.Toggle(request.PickThemes, "Choose themes", UiSkin.Toggle);
            if (request.PickThemes)
                DrawChoices(_themeSearch = Search(_themeSearch), Catalog.Current?.Themes, request.ThemeIds, ref _themeScroll);

            Section("Player tokens");
            GUILayout.Label("Random uses every token you have. A chosen list uses only those. They can stand anywhere on the generated map, or only inside an area you drag.", UiSkin.MutedLabel);
            request.PickPlayers = GUILayout.Toggle(request.PickPlayers, "Choose player tokens", UiSkin.Toggle);
            if (request.PickPlayers)
                DrawTokens(_playerSearch = Search(_playerSearch), request.PlayerIds, ref _playerScroll);
            Slider("Player tokens", ref request.PlayerCount, 0, 24, "");
            int place = request.PlayersInArea ? 1 : 0;
            place = GUILayout.SelectionGrid(place, new[] { "Anywhere on the map", "Inside a chosen area" }, 1, UiSkin.ButtonStyle);
            request.PlayersInArea = place == 1;
            if (request.PlayersInArea)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(request.HasArea ? "Choose a different area" : "Choose area on the map", UiSkin.ButtonStyle))
                    AreaPicker.Begin();
                if (request.HasArea && GUILayout.Button("Clear area", UiSkin.ButtonStyle, GUILayout.Width(110f)))
                    request.HasArea = false;
                GUILayout.EndHorizontal();
                if (request.HasArea)
                    GUILayout.Label($"Area starts at {request.AreaX}, {request.AreaY} and is {request.AreaW} by {request.AreaH} tiles.", UiSkin.MutedLabel);
            }

            Section("Enemy tokens");
            GUILayout.Label("These are placed on the floors of the buildings. They do not clear fog.", UiSkin.MutedLabel);
            request.PickEnemies = GUILayout.Toggle(request.PickEnemies, "Choose enemy tokens", UiSkin.Toggle);
            if (request.PickEnemies)
                DrawTokens(_enemySearch = Search(_enemySearch), request.EnemyIds, ref _enemyScroll);
            Slider("Enemy tokens", ref request.EnemyCount, 0, 40, "");

            Section("Fog");
            request.Fog = GUILayout.Toggle(request.Fog, "Cover the map with fog when generating", UiSkin.Toggle);
            Slider("Vision distance", ref request.VisionTiles, 1, 20, "tiles");
            FogOverlay.SetRadius(request.VisionTiles);
            GUILayout.Label("Cover and hide change the open map only. Nothing already placed is moved or deleted. Choose player tokens above if only those should clear fog. Otherwise any token does. Fog is not saved inside the map, and it does not export to a virtual tabletop.", UiSkin.MutedLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cover this map", UiSkin.ButtonStyle))
            {
                if (FogOverlay.ShowOnMap(request.VisionTiles, PlayerBlocks(request)))
                    Session.Status = request.PickPlayers && request.PlayerIds.Count > 0
                        ? "Fog covers this map. Moving a chosen player token clears it."
                        : "Fog covers this map. Moving any token clears it.";
                else
                    Session.Status = "Open a map first.";
            }
            if (GUILayout.Button("Hide fog", UiSkin.ButtonStyle))
            {
                if (FogOverlay.Hide())
                    Session.Status = "Fog is hidden. The map is unchanged.";
                else
                    Session.Status = "Open a map first.";
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset revealed fog", UiSkin.ButtonStyle))
                FogOverlay.ResetCover();

            Section("Workshop");
            if (WorkshopBridge.IsLoaded)
            {
                request.ForceWorkshop = GUILayout.Toggle(request.ForceWorkshop, "Replace matching assets with Workshop assets", UiSkin.Toggle);
                GUILayout.Label("For this run, props, floors, walls, doors, and windows use a matching Workshop asset whenever one exists. Roofs stay as the game built them. Your F6 chances are put back when generation finishes.", UiSkin.MutedLabel);
            }
            else
            {
                GUILayout.Label("Workshop Procgen is not installed, so generation uses the game's own assets.", UiSkin.MutedLabel);
            }

            Section("Map");
            request.ClearRooms = GUILayout.Toggle(request.ClearRooms, "Remove existing rooms first", UiSkin.Toggle);
            if (!request.ClearRooms)
                GUILayout.Label("New buildings are placed only on open tiles.", UiSkin.MutedLabel);
            if (Session.ConfirmClear)
                GUILayout.Label("This removes the rooms already on the map. Click Generate again to continue.", UiSkin.Body);

            if (!string.IsNullOrEmpty(Session.Status))
                GUILayout.Label(Session.Status, UiSkin.Body);
            GUI.enabled = !Session.Busy;
            string generate = Session.Busy ? "Generating…" : "Generate";
            if (GUILayout.Button(generate, UiSkin.Primary))
            {
                try { Generate(request); }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Generate could not start: {e}");
                    Session.Status = "Generate could not start. See the BepInEx log.";
                    Session.Busy = false;
                }
            }
            GUI.enabled = true;

            if (!_catalogReady)
                GUILayout.Label("Theme and token lists fill in once a map is open.", UiSkin.MutedLabel);

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
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

        private void Generate(BuildRequest request)
        {
            var map = GameRefs.Map;
            if (map == null)
            {
                Session.Status = "Open a map first.";
                return;
            }
            if (request.PlayersInArea && !request.HasArea)
            {
                Session.Status = "Choose an area on the map, or place player tokens anywhere.";
                return;
            }
            if (request.ClearRooms && map.HasNormalRoom() && !Session.ConfirmClear)
            {
                Session.ConfirmClear = true;
                Session.Status = "The rooms on this map will be removed.";
                return;
            }
            Session.ConfirmClear = false;
            StartCoroutine(BuildRunner.Run(request));
        }

        private static void Section(string title)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, UiSkin.Section);
        }

        private static void Slider(string label, ref int value, int min, int max, string unit)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, UiSkin.Body, GUILayout.Width(160f));
            int next = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
            value = Mathf.Clamp(next, min, max);
            GUILayout.Label(unit.Length == 0 ? value.ToString() : value + " " + unit, UiSkin.Value, GUILayout.Width(72f));
            GUILayout.EndHorizontal();
        }

        private static string Search(string value)
        {
            return GUILayout.TextField(value ?? "", UiSkin.Search);
        }

        private static void DrawChoices(string search, List<ThemeChoice> themes, HashSet<string> selected, ref Vector2 scroll)
        {
            if (themes == null || themes.Count == 0)
            {
                GUILayout.Label("No themes are available yet.", UiSkin.MutedLabel);
                return;
            }
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(140f));
            foreach (var theme in themes)
            {
                if (!Matches(theme.Name, search))
                    continue;
                bool on = selected.Contains(theme.Id);
                bool next = GUILayout.Toggle(on, theme.Name, UiSkin.Toggle);
                if (next != on)
                {
                    if (next) selected.Add(theme.Id);
                    else selected.Remove(theme.Id);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Label(selected.Count == 0 ? "Nothing selected, so a theme is chosen at random." : selected.Count + " selected.", UiSkin.MutedLabel);
        }

        private static void DrawTokens(string search, HashSet<string> selected, ref Vector2 scroll)
        {
            var tokens = Catalog.Current?.Tokens;
            if (tokens == null || tokens.Count == 0)
            {
                GUILayout.Label("No tokens are available yet.", UiSkin.MutedLabel);
                return;
            }
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(160f));
            string group = null;
            foreach (var token in tokens)
            {
                if (!Matches(token.Name, search) && !Matches(token.Group, search))
                    continue;
                if (group != token.Group)
                {
                    group = token.Group;
                    GUILayout.Label(group, UiSkin.MutedLabel);
                }
                bool on = selected.Contains(token.Id);
                bool next = GUILayout.Toggle(on, token.Name, UiSkin.Toggle);
                if (next != on)
                {
                    if (next) selected.Add(token.Id);
                    else selected.Remove(token.Id);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Label(selected.Count == 0 ? "Nothing selected, so a token is chosen at random." : selected.Count + " selected.", UiSkin.MutedLabel);
        }

        private static bool Matches(string text, string search)
        {
            if (string.IsNullOrEmpty(search))
                return true;
            return text != null && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
