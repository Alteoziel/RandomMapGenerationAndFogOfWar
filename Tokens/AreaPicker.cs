using UnityEngine;

namespace MapGenerator
{
    /// <summary>
    /// Drag a rectangle on the map. The window stays closed until the drag ends.
    /// </summary>
    internal sealed class AreaPicker : MonoBehaviour
    {
        private bool _down;
        private Vector2 _start;
        private GameObject _preview;
        private Mesh _mesh;
        private Material _material;

        public static void Begin()
        {
            Session.PickingArea = true;
            Session.WindowOpen = false;
            Session.ConfirmClear = false;
            Session.Status = "Drag a rectangle on the map. Right-click or Esc cancels.";
        }

        private void OnGUI()
        {
            if (!Session.PickingArea)
                return;
            UiSkin.Ensure();
            var banner = new Rect(16f, 16f, 520f, 36f);
            GUI.Box(banner, GUIContent.none, UiSkin.CardStyle);
            GUI.Label(new Rect(28f, 22f, 496f, 24f), "Drag a rectangle for the player tokens. Right-click or Esc cancels.", UiSkin.Body);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Cancel();
                Event.current.Use();
            }
        }

        private void Update()
        {
            if (!Session.PickingArea)
            {
                HidePreview();
                return;
            }
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }
            if (!TryMapPoint(out var point))
                return;

            if (Input.GetMouseButtonDown(0))
            {
                _down = true;
                _start = point;
                ShowPreview(_start, point);
            }
            else if (_down && Input.GetMouseButton(0))
            {
                ShowPreview(_start, point);
            }
            else if (_down && Input.GetMouseButtonUp(0))
            {
                _down = false;
                Finish(_start, point);
            }
        }

        private void OnDestroy()
        {
            if (_preview != null)
                Destroy(_preview);
            if (_mesh != null)
                Destroy(_mesh);
            if (_material != null)
                Destroy(_material);
        }

        private static bool TryMapPoint(out Vector2 point)
        {
            point = default;
            var view = GameRefs.View;
            var camera = Camera.main;
            if (view == null || camera == null)
                return false;
            var ray = camera.ScreenPointToRay(Input.mousePosition);
            var hit = view.GetMapPosition(ray);
            if (!hit.HasValue)
                return false;
            point = hit.Value;
            return true;
        }

        private void Finish(Vector2 a, Vector2 b)
        {
            var request = Session.Request;
            var map = GameRefs.Map;
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y));
            int x1 = Mathf.Max(x0 + 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x)));
            int y1 = Mathf.Max(y0 + 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y)));
            int limitX = map != null ? map.width : 0;
            int limitY = map != null ? map.height : 0;
            if (limitX > 1 && limitY > 1)
            {
                x0 = Mathf.Clamp(x0, 0, limitX - 1);
                y0 = Mathf.Clamp(y0, 0, limitY - 1);
                x1 = Mathf.Clamp(x1, x0 + 1, limitX);
                y1 = Mathf.Clamp(y1, y0 + 1, limitY);
            }
            request.HasArea = true;
            request.AreaX = x0;
            request.AreaY = y0;
            request.AreaW = x1 - x0;
            request.AreaH = y1 - y0;
            request.PlayersInArea = true;
            Session.PickingArea = false;
            Session.WindowOpen = true;
            Session.Status = $"Player area is {request.AreaW} by {request.AreaH} tiles.";
            HidePreview();
        }

        private void Cancel()
        {
            _down = false;
            Session.PickingArea = false;
            Session.WindowOpen = true;
            Session.Status = "Area choice cancelled.";
            HidePreview();
        }

        private void HidePreview()
        {
            if (_preview != null)
                _preview.SetActive(false);
        }

        private void ShowPreview(Vector2 a, Vector2 b)
        {
            var view = GameRefs.View;
            var map = GameRefs.Map;
            var foundation = map != null ? map.surfaceTerrainBaseFoundation : null;
            if (view == null || foundation == null)
                return;
            EnsurePreview(view);
            _preview.SetActive(true);
            _preview.transform.SetParent(view.transform, false);
            _preview.layer = view.normalLayer;

            float minX = Mathf.Min(a.x, b.x);
            float minY = Mathf.Min(a.y, b.y);
            float maxX = Mathf.Max(a.x, b.x);
            float maxY = Mathf.Max(a.y, b.y);
            if (maxX - minX < 0.2f)
                maxX = minX + 1f;
            if (maxY - minY < 0.2f)
                maxY = minY + 1f;

            Vector3 Corner(float x, float y)
            {
                var mapPos = new Vector2(x, y);
                float height = 0.4f;
                try
                {
                    height = map.GetPlacementHeight(foundation, foundation.MapToFoundation(mapPos), out _) + 0.35f;
                }
                catch { /* preview still draws */ }
                return view.transform.InverseTransformPoint(view.MapToWorld(mapPos, height));
            }

            var verts = new[] { Corner(minX, minY), Corner(minX, maxY), Corner(maxX, maxY), Corner(maxX, minY) };
            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            _mesh.uv = new[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
            _mesh.RecalculateBounds();
        }

        private void EnsurePreview(DungeonAlchemist.MapViewing.MapView view)
        {
            if (_preview != null)
                return;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard");
            _material = new Material(shader) { color = new Color(0.86f, 0.68f, 0.40f, 0.45f), hideFlags = HideFlags.HideAndDontSave };
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _material.mainTexture = tex;
            _material.renderQueue = 3600;
            _mesh = new Mesh { name = "MapGeneratorArea", hideFlags = HideFlags.HideAndDontSave };
            _preview = new GameObject("MapGeneratorArea");
            _preview.hideFlags = HideFlags.HideAndDontSave;
            _preview.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _preview.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _preview.transform.SetParent(view.transform, false);
        }
    }
}
