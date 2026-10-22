using UnityEngine;

namespace MapGenerator
{
    internal static class UiSkin
    {
        public static readonly Color Ink = new Color(0.95f, 0.91f, 0.84f);
        public static readonly Color Muted = new Color(0.67f, 0.61f, 0.52f);
        public static readonly Color Gold = new Color(0.86f, 0.68f, 0.40f);
        public static readonly Color Panel = new Color(0.16f, 0.14f, 0.12f, 0.98f);
        public static readonly Color Card = new Color(0.22f, 0.19f, 0.16f, 1f);
        public static readonly Color CardEdge = new Color(0.40f, 0.32f, 0.22f, 1f);
        public static readonly Color Button = new Color(0.28f, 0.24f, 0.19f, 1f);
        public static readonly Color ButtonHot = new Color(0.36f, 0.30f, 0.22f, 1f);
        public static readonly Color Danger = new Color(0.55f, 0.24f, 0.18f, 1f);

        public static GUISkin Skin;
        public static GUIStyle Title;
        public static GUIStyle Section;
        public static GUIStyle Body;
        public static GUIStyle MutedLabel;
        public static GUIStyle Value;
        public static GUIStyle ButtonStyle;
        public static GUIStyle Primary;
        public static GUIStyle Toggle;
        public static GUIStyle Search;
        public static GUIStyle CardStyle;
        public static Texture2D Pixel;
        public static Texture2D CardTex;
        public static Texture2D ButtonTex;
        public static Texture2D ButtonHotTex;
        public static Texture2D PrimaryTex;
        public static Texture2D FieldTex;

        private static bool _ready;

        public static void Ensure()
        {
            if (_ready)
                return;

            Pixel = Solid(Panel);
            CardTex = Solid(Card);
            ButtonTex = Solid(Button);
            ButtonHotTex = Solid(ButtonHot);
            PrimaryTex = Solid(new Color(0.45f, 0.32f, 0.16f, 1f));
            FieldTex = Solid(new Color(0.11f, 0.10f, 0.09f, 1f));

            Skin = Object.Instantiate(GUI.skin);
            Skin.window.normal.background = Pixel;
            Skin.window.onNormal.background = Pixel;
            Skin.window.normal.textColor = Gold;
            Skin.window.onNormal.textColor = Gold;
            Skin.window.fontSize = 16;
            Skin.window.alignment = TextAnchor.UpperLeft;
            Skin.window.padding = new RectOffset(16, 16, 28, 16);
            Skin.box.normal.background = CardTex;
            Skin.horizontalSlider.normal.background = FieldTex;
            Skin.horizontalSliderThumb.normal.background = PrimaryTex;
            Skin.horizontalSliderThumb.fixedWidth = 14f;
            Skin.horizontalSliderThumb.fixedHeight = 16f;

            Title = new GUIStyle(Skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            Title.normal.textColor = Gold;
            Section = new GUIStyle(Skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            Section.normal.textColor = Gold;
            Body = new GUIStyle(Skin.label) { fontSize = 13, wordWrap = true };
            Body.normal.textColor = Ink;
            MutedLabel = new GUIStyle(Body);
            MutedLabel.normal.textColor = Muted;
            Value = new GUIStyle(Body) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            Value.normal.textColor = Gold;

            ButtonStyle = new GUIStyle(Skin.button) { fontSize = 13 };
            Paint(ButtonStyle, ButtonTex, ButtonHotTex, Ink);
            Primary = new GUIStyle(ButtonStyle) { fontSize = 16, fontStyle = FontStyle.Bold, fixedHeight = 40f };
            Paint(Primary, PrimaryTex, Solid(new Color(0.56f, 0.40f, 0.20f, 1f)), new Color(1f, 0.95f, 0.86f));

            Toggle = new GUIStyle(Skin.toggle) { fontSize = 13, wordWrap = true };
            Toggle.normal.textColor = Ink;
            Toggle.onNormal.textColor = Ink;
            Toggle.hover.textColor = Color.white;
            Toggle.onHover.textColor = Color.white;

            Search = new GUIStyle(Skin.textField) { fontSize = 13 };
            Search.normal.background = FieldTex;
            Search.focused.background = FieldTex;
            Search.normal.textColor = Ink;
            Search.focused.textColor = Ink;

            CardStyle = new GUIStyle(Skin.box) { padding = new RectOffset(12, 12, 10, 10), margin = new RectOffset(0, 0, 6, 6) };
            CardStyle.normal.background = CardTex;
            _ready = true;
        }

        private static void Paint(GUIStyle style, Texture2D normal, Texture2D hot, Color text)
        {
            style.normal.background = normal;
            style.hover.background = hot;
            style.active.background = hot;
            style.onNormal.background = normal;
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            style.onNormal.textColor = text;
        }

        private static Texture2D Solid(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }
}
