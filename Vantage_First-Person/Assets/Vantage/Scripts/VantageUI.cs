using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vantage
{
    /// <summary>
    /// Shared building blocks for the code-built UI (HUD and minimap): one font, one palette, canvases,
    /// rects, images, texts and a few generated sprites.
    /// </summary>
    public static class VantageUI
    {
        public static readonly Color Accent = new Color(1f, 0.74f, 0.3f);
        public static readonly Color Danger = new Color(0.94f, 0.24f, 0.16f);
        public static readonly Color Muted = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color Faint = new Color(1f, 1f, 1f, 0.16f);

        /// <summary>Extra space between words, in em/100: Bebas' own space is so narrow that words run together.</summary>
        private const float WordSpacing = 28f;

        private static TMP_FontAsset _font;
        private static Material _shadowed;

        /// <summary>
        /// Bebas (copied from the template's fonts to Resources/VantageFont) as a dynamic TextMeshPro font, built once
        /// at runtime; glyphs Bebas lacks (∞) come from TMP's default font. Falls back to that font entirely.
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;
                var source = Resources.Load<Font>("VantageFont");
                _font = source != null ? TMP_FontAsset.CreateFontAsset(source) : null;
                var fallback = TMP_Settings.defaultFontAsset;
                if (_font == null) _font = fallback;
                else if (fallback != null) _font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
                return _font;
            }
        }

        /// <summary>The font's material with a soft drop shadow (underlay), shared by every HUD text.</summary>
        private static Material Shadowed
        {
            get
            {
                if (_shadowed != null || Font == null) return _shadowed;
                _shadowed = new Material(Font.material) { name = "Vantage UI Text (shadow)" };
                _shadowed.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                _shadowed.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0, 0, 0, 0.55f));
                _shadowed.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
                _shadowed.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
                _shadowed.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
                return _shadowed;
            }
        }

        public static string Caps(string s) => string.IsNullOrEmpty(s) ? "" : s.ToUpperInvariant();

        public static RectTransform Canvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        /// <summary>A rect anchored (and pivoted) at 'anchor' of its parent, e.g. (0,0) bottom-left, (0.5,1) top-centre.</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.anchoredPosition = position;
            r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var r = Rect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            return r;
        }

        public static Image Image(RectTransform r, Color color, Sprite sprite = null)
        {
            var i = r.gameObject.AddComponent<Image>();
            i.sprite = sprite;
            i.color = color;
            i.raycastTarget = false;
            return i;
        }

        public static TMP_Text Text(Transform parent, string name, float size, TextAlignmentOptions alignment, Vector2 anchor, Vector2 position, Vector2 box)
        {
            var r = Rect(name, parent, anchor, position, box);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            if (Shadowed != null) t.fontSharedMaterial = Shadowed;
            t.fontSize = size;
            t.alignment = alignment;
            t.color = Color.white;
            t.wordSpacing = WordSpacing;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A horizontal bar: background, an optional trailing layer and the fill (Image.fillAmount).</summary>
        public static Image Bar(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color fill, out Image trail)
        {
            var back = Rect(name, parent, anchor, position, size);
            Image(back, new Color(0, 0, 0, 0.45f));
            trail = filled(Image(Stretch("Trail", back), new Color(1, 1, 1, 0.5f), Pixel));
            return filled(Image(Stretch("Fill", back), fill, Pixel));
        }

        private static Image filled(Image i)
        {
            i.type = UnityEngine.UI.Image.Type.Filled;
            i.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            return i;
        }

        private static Sprite _pixel, _circle, _shade, _vignette;

        /// <summary>Plain white sprite (needed for filled images).</summary>
        public static Sprite Pixel => _pixel != null ? _pixel : _pixel = make(4, (u, v) => 1f);

        public static Sprite Circle => _circle != null ? _circle : _circle = make(32, (u, v) => Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v)) * 6f));

        /// <summary>Soft dark blob behind HUD clusters, so text reads on bright scenes without hard panels.</summary>
        public static Sprite Shade => _shade != null ? _shade : _shade = make(64, (u, v) => Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(Mathf.Sqrt(u * u + v * v))));

        /// <summary>Transparent in the middle, opaque at the edges.</summary>
        public static Sprite Vignette => _vignette != null ? _vignette : _vignette = make(128, (u, v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, Mathf.Sqrt(u * u + v * v) / Mathf.Sqrt(2f))));

        /// <summary>Generated white sprite whose alpha is alpha(u, v), u and v in -1..1.</summary>
        private static Sprite make(int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1, 1, 1, alpha((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f));
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
