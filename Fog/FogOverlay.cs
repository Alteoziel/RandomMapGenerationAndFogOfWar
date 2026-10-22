using System.Collections.Generic;
using DungeonAlchemist.Data;
using DungeonAlchemist.MapEditing;
using DungeonAlchemist.MapViewing;
using UnityEngine;
using UnityEngine.Rendering;

namespace MapGenerator
{
    /// <summary>
    /// Dark cells over the map grid. They are not map objects, so save and VTT export leave them out.
    /// Revealed cells stay revealed until fog is reset.
    /// </summary>
    internal sealed class FogOverlay : MonoBehaviour
    {
        private static FogOverlay _instance;

        private FogState _state;
        private readonly HashSet<long> _revealed = new HashSet<long>();
        private readonly HashSet<string> _playerInstances = new HashSet<string>();
        private readonly HashSet<string> _playerBlocks = new HashSet<string>();
        private string _loadedKey;
        private GameObject _quad;
        private Mesh _mesh;
        private Material _material;
        private bool _meshDirty = true;
        private bool _saveDirty;
        private float _nextSave;

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            DestroyVisuals();
        }

        public static void Cover(int radius, ICollection<string> playerInstances, ICollection<string> playerBlocks)
        {
            if (_instance == null)
                return;
            var map = GameRefs.Map;
            var key = GameRefs.MapKey;
            if (map == null || string.IsNullOrEmpty(key))
                return;

            var state = new FogState
            {
                MapKey = key,
                Enabled = true,
                Radius = Mathf.Max(1, radius),
                Width = map.width,
                Height = map.height,
            };
            if (playerInstances != null)
                state.PlayerInstanceIds.AddRange(playerInstances);
            if (playerBlocks != null)
                state.PlayerBlockIds.AddRange(playerBlocks);
            _instance._loadedKey = key;
            _instance.Apply(state, save: true);
        }

        public static bool ShowOnMap(int radius, ICollection<string> playerBlocks)
        {
            if (_instance == null)
                return false;
            var map = GameRefs.Map;
            var key = GameRefs.MapKey;
            if (map == null || string.IsNullOrEmpty(key))
                return false;

            bool anyToken = playerBlocks == null || playerBlocks.Count == 0;
            var state = _instance._state != null && _instance._state.MapKey == key
                ? _instance._state
                : FogState.Load(key) ?? new FogState();
            state.MapKey = key;
            state.Enabled = true;
            state.AnyTokenClears = anyToken;
            state.Radius = Mathf.Max(1, radius);
            state.Width = map.width;
            state.Height = map.height;
            state.Revealed = state.Revealed ?? new List<FogCell>();
            state.PlayerInstanceIds = state.PlayerInstanceIds ?? new List<string>();
            state.PlayerBlockIds = new List<string>();
            if (!anyToken)
                state.PlayerBlockIds.AddRange(playerBlocks);
            _instance._loadedKey = key;
            _instance.Apply(state, save: true);
            return true;
        }

        public static bool Hide()
        {
            if (_instance == null)
                return false;
            var key = GameRefs.MapKey;
            if (string.IsNullOrEmpty(key))
                return false;
            var state = _instance._state != null && _instance._state.MapKey == key
                ? _instance._state
                : FogState.Load(key) ?? new FogState { MapKey = key };
            state.MapKey = key;
            state.Enabled = false;
            state.Revealed = state.Revealed ?? new List<FogCell>();
            state.PlayerInstanceIds = state.PlayerInstanceIds ?? new List<string>();
            state.PlayerBlockIds = state.PlayerBlockIds ?? new List<string>();
            _instance._loadedKey = key;
            _instance.Apply(state, save: true);
            return true;
        }

        public static void ClearSaved()
        {
            if (_instance == null)
                return;
            FogState.Delete(GameRefs.MapKey);
            _instance._state = null;
            _instance._revealed.Clear();
            _instance._playerInstances.Clear();
            _instance._playerBlocks.Clear();
            _instance._meshDirty = true;
            _instance._saveDirty = false;
        }

        public static void ResetCover()
        {
            if (_instance == null || _instance._state == null || !_instance._state.Enabled)
                return;
            _instance._revealed.Clear();
            _instance._state.Revealed.Clear();
            var map = GameRefs.Map;
            if (map != null)
            {
                _instance._state.Width = map.width;
                _instance._state.Height = map.height;
            }
            _instance._meshDirty = true;
            _instance._saveDirty = true;
            _instance._nextSave = Time.unscaledTime + 0.2f;
        }

        public static int ActiveRadius
        {
            get
            {
                var state = _instance != null ? _instance._state : null;
                return state != null && state.Enabled ? state.Radius : 0;
            }
        }

        public static void SetRadius(int radius)
        {
            if (_instance == null || _instance._state == null)
                return;
            radius = Mathf.Max(1, radius);
            if (_instance._state.Radius == radius)
                return;
            _instance._state.Radius = radius;
            _instance._saveDirty = true;
            _instance._nextSave = Time.unscaledTime + 0.4f;
        }

        public static void OnMoved(MoveObjectOperation operation)
        {
            if (_instance == null || operation == null || operation.result != PlaceObjectOperationResult.Success)
                return;
            var instance = operation.movedInstance;
            if (instance == null || _instance._state == null || !_instance._state.Enabled)
                return;
            bool listed = _instance._playerInstances.Contains(instance.id) || _instance._playerBlocks.Contains(instance.blockId);
            if (!listed && _instance._state.AnyTokenClears)
                listed = IsPlayerToken(instance.blockId);
            if (!listed)
                return;

            int radius = Mathf.Max(1, _instance._state.Radius);
            int added = _instance.Reveal(operation.newPos, radius);
            if (added <= 0)
                return;
            _instance._meshDirty = true;
            _instance._saveDirty = true;
            _instance._nextSave = Time.unscaledTime + 0.35f;
        }

        private void Update()
        {
            var key = GameRefs.MapKey;
            if (key != _loadedKey)
            {
                _loadedKey = key;
                Apply(FogState.Load(key), save: false);
            }

            if (_saveDirty && Time.unscaledTime >= _nextSave && _state != null)
            {
                _saveDirty = false;
                try { _state.Save(); }
                catch (System.Exception e) { Plugin.Log.LogWarning($"Could not save fog: {e.Message}"); }
            }
        }

        private void LateUpdate()
        {
            if (!_meshDirty)
                return;
            _meshDirty = false;
            try { Rebuild(); }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Fog could not be drawn: {e.Message}");
            }
        }

        private void Apply(FogState state, bool save)
        {
            _state = state;
            _revealed.Clear();
            _playerInstances.Clear();
            _playerBlocks.Clear();
            if (state != null)
            {
                foreach (var cell in state.Revealed)
                    _revealed.Add(Key(cell.X, cell.Y));
                foreach (var id in state.PlayerInstanceIds)
                    if (!string.IsNullOrEmpty(id))
                        _playerInstances.Add(id);
                foreach (var id in state.PlayerBlockIds)
                    if (!string.IsNullOrEmpty(id))
                        _playerBlocks.Add(id);
            }
            _meshDirty = true;
            if (save && state != null)
            {
                _saveDirty = true;
                _nextSave = Time.unscaledTime + 0.1f;
            }
        }

        private int Reveal(Vector2 mapPos, int radius)
        {
            var map = GameRefs.Map;
            if (map == null || _state == null)
                return 0;
            int width = map.width;
            int height = map.height;
            _state.Width = width;
            _state.Height = height;
            int added = 0;
            int minX = Mathf.Max(0, Mathf.FloorToInt(mapPos.x - radius));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(mapPos.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(mapPos.y - radius));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(mapPos.y + radius));
            float limit = radius;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var center = new Vector2(x + 0.5f, y + 0.5f);
                    if (Vector2.Distance(mapPos, center) > limit)
                        continue;
                    if (!_revealed.Add(Key(x, y)))
                        continue;
                    _state.Revealed.Add(new FogCell { X = x, Y = y });
                    added++;
                }
            }
            return added;
        }

        private void Rebuild()
        {
            var map = GameRefs.Map;
            var view = GameRefs.View;
            var foundation = map != null ? map.surfaceTerrainBaseFoundation : null;
            bool show = _state != null && _state.Enabled && map != null && view != null && foundation != null;
            if (!show)
            {
                if (_quad != null)
                    _quad.SetActive(false);
                return;
            }

            EnsureVisual(view);
            _quad.SetActive(true);
            AlignToGrid(view);

            int width = map.width;
            int height = map.height;
            var vertices = new List<Vector3>(width * height);
            var triangles = new List<int>(width * height * 12);
            var colors = new List<Color>(width * height);
            var uvs = new List<Vector2>(width * height);
            var color = new Color(0.02f, 0.016f, 0.03f, 0.9f);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (_revealed.Contains(Key(x, y)))
                        continue;
                    var center = new Vector2(x + 0.5f, y + 0.5f);
                    int start = vertices.Count;
                    if (Inside(foundation, center))
                    {
                        float lid = Floor(map, foundation, center) + 1.5f;
                        vertices.Add(view.MapToWorld(new Vector3(x, lid, y)));
                        vertices.Add(view.MapToWorld(new Vector3(x, lid, y + 1f)));
                        vertices.Add(view.MapToWorld(new Vector3(x + 1f, lid, y + 1f)));
                        vertices.Add(view.MapToWorld(new Vector3(x + 1f, lid, y)));
                    }
                    else
                    {
                        float mid = Ground(map, foundation, center);
                        vertices.Add(view.MapToWorld(new Vector3(x, Ground(map, foundation, new Vector2(x, y), mid) + 0.1f, y)));
                        vertices.Add(view.MapToWorld(new Vector3(x, Ground(map, foundation, new Vector2(x, y + 1f), mid) + 0.1f, y + 1f)));
                        vertices.Add(view.MapToWorld(new Vector3(x + 1f, Ground(map, foundation, new Vector2(x + 1f, y + 1f), mid) + 0.1f, y + 1f)));
                        vertices.Add(view.MapToWorld(new Vector3(x + 1f, Ground(map, foundation, new Vector2(x + 1f, y), mid) + 0.1f, y)));
                    }
                    colors.Add(color);
                    colors.Add(color);
                    colors.Add(color);
                    colors.Add(color);
                    uvs.Add(new Vector2(0f, 0f));
                    uvs.Add(new Vector2(0f, 1f));
                    uvs.Add(new Vector2(1f, 1f));
                    uvs.Add(new Vector2(1f, 0f));
                    triangles.Add(start);
                    triangles.Add(start + 1);
                    triangles.Add(start + 2);
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 3);
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 1);
                    triangles.Add(start);
                    triangles.Add(start + 3);
                    triangles.Add(start + 2);
                }
            }

            _mesh.Clear();
            if (vertices.Count == 0)
            {
                _quad.SetActive(false);
                return;
            }
            _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetUVs(0, uvs);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            Plugin.Log.LogInfo($"Fog covers {width}x{height} on layer {_quad.layer} with shader '{_material.shader.name}'.");
        }

        private static bool IsPlayerToken(string blockId)
        {
            try
            {
                var map = GameRefs.Map;
                var config = map != null ? map.database.BuildingBlockConfigs.GetOrNull(blockId) : null;
                return config != null && config.IsToken();
            }
            catch
            {
                return false;
            }
        }

        private static bool Inside(Foundation foundation, Vector2 mapPos)
        {
            try
            {
                return foundation != null && foundation.IsInNormalRoom(mapPos);
            }
            catch
            {
                return false;
            }
        }

        private static float Floor(Map map, Foundation foundation, Vector2 mapPos)
        {
            float y = foundation != null ? foundation.GetRoomHeight() : 0f;
            try
            {
                if (foundation == null)
                    return y;
                float sampled = map.GetPlacementHeight(foundation, foundation.MapToFoundation(mapPos), out bool valid);
                if (valid && sampled > -1000f)
                    y = sampled;
            }
            catch
            {
                /* the room plane is enough */
            }
            return y;
        }

        private static float Ground(Map map, Foundation foundation, Vector2 mapPos, float fallback = float.NaN)
        {
            try
            {
                if (foundation == null)
                    return float.IsNaN(fallback) ? 0f : fallback;
                if (foundation.IsInNormalRoom(mapPos))
                    return float.IsNaN(fallback) ? foundation.GetRoomHeight() : fallback;
                float sampled = map.GetPlacementHeight(foundation, foundation.MapToFoundation(mapPos), out bool valid);
                if (valid && sampled > -1000f)
                    return sampled;
            }
            catch
            {
                /* use the tile center */
            }
            if (!float.IsNaN(fallback))
                return fallback;
            return foundation != null ? foundation.GetRoomHeight() : 0f;
        }

        private void AlignToGrid(MapView view)
        {
            var grid = FindObjectOfType<GridRenderer>();
            if (grid != null)
            {
                _quad.transform.SetParent(grid.transform.parent, false);
                _quad.transform.localPosition = grid.transform.localPosition;
                _quad.transform.localRotation = grid.transform.localRotation;
                _quad.transform.localScale = grid.transform.localScale;
                _quad.layer = grid.gameObject.layer;
                return;
            }
            _quad.transform.SetParent(view.transform, false);
            _quad.transform.localPosition = Vector3.zero;
            _quad.transform.localRotation = Quaternion.identity;
            _quad.transform.localScale = Vector3.one;
            _quad.layer = view.normalLayer;
        }

        private void EnsureVisual(MapView view)
        {
            if (_quad != null)
                return;
            _material = CreateMaterial();
            _material.hideFlags = HideFlags.HideAndDontSave;
            _mesh = new Mesh { name = "MapGeneratorFog", hideFlags = HideFlags.HideAndDontSave };
            _quad = new GameObject("MapGeneratorFog");
            _quad.hideFlags = HideFlags.HideAndDontSave;
            var filter = _quad.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;
            var renderer = _quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _quad.transform.SetParent(view.transform, false);
        }

        private void DestroyVisuals()
        {
            if (_quad != null)
                Destroy(_quad);
            if (_mesh != null)
                Destroy(_mesh);
            if (_material != null)
                Destroy(_material);
            _quad = null;
            _mesh = null;
            _material = null;
        }

        private static Material CreateMaterial()
        {
            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            if (shader == null)
                throw new System.InvalidOperationException("No shader was available for the fog.");
            var material = new Material(shader);
            material.mainTexture = White();
            material.color = new Color(0.02f, 0.016f, 0.03f, 0.9f);
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 0);
            if (shader.name == "Standard")
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            material.renderQueue = 4000;
            return material;
        }

        private static Texture2D White()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "MapGeneratorFogWhite"
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return tex;
        }

        private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
