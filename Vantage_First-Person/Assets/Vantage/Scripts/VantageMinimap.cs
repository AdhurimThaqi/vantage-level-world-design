using System.Collections.Generic;
using CoverShooter;
using UnityEngine;
using UnityEngine.UI;

namespace Vantage
{
    /// <summary>
    /// Corner minimap, top-left. North is up; the map scrolls under a fixed arrow that turns with the player.
    /// The map is a top-down image of the world baked by Vantage → World (VantageMilitaryBase), so it costs one
    /// texture instead of a second camera. Shows the tower (pinned to the edge when it is out of view, so the goal
    /// is always on screen), nearby living enemies as red dots, the distance to the tower and the current space.
    /// Built in code like VantagePlayerHUD; the scene object only carries the settings.
    /// </summary>
    public class VantageMinimap : MonoBehaviour
    {
        [Tooltip("Top-down image of WorldArea, baked by Vantage → World → Build Military World.")]
        public Texture2D Map;
        [Tooltip("World x/z rectangle the map image covers.")]
        public Rect WorldArea;
        public Transform Tower;

        [Tooltip("Width of the world shown in the minimap, in metres.")]
        public float ViewMetres = 160f;
        [Tooltip("Enemies closer than this are shown.")]
        public float EnemyRange = 55f;
        public float SizePixels = 230f;
        public Color Accent = new Color(0.96f, 0.66f, 0.25f);
        public Color Enemy = new Color(0.95f, 0.22f, 0.15f);

        private const int MaxDots = 24;

        private Transform _player;
        private RectTransform _frame;
        private RawImage _map;
        private RectTransform _arrow, _tower;
        private Text _distance, _place;
        private readonly List<Image> _dots = new List<Image>();
        private readonly List<BaseActor> _soldiers = new List<BaseActor>();
        private float _nextScan;
        private VantagePlaytestLogger _logger;

        private void Start()
        {
            if (Map == null || WorldArea.width <= 0)
            {
                enabled = false;
                return;
            }
            build();
            _logger = FindFirstObjectByType<VantagePlaytestLogger>();
        }

        private void LateUpdate()
        {
            if (_player == null)
            {
                var p = FindFirstObjectByType<VantageThirdPersonPlayer>();
                if (p == null)
                    return;
                _player = p.transform;
            }

            var pos = _player.position;
            var view = ViewMetres / WorldArea.width;
            var u = (pos.x - WorldArea.x) / WorldArea.width;
            var v = (pos.z - WorldArea.y) / WorldArea.height;
            _map.uvRect = new Rect(u - view / 2, v - view / 2, view, view * WorldArea.width / WorldArea.height);

            _arrow.localEulerAngles = new Vector3(0, 0, -_player.eulerAngles.y);

            if (Tower != null)
            {
                var offset = toMap(Tower.position - pos, out var inside);
                _tower.anchoredPosition = offset;
                _tower.localScale = Vector3.one * (inside ? 1f : 0.8f);
                var metres = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(Tower.position.x, Tower.position.z));
                _distance.text = metres < 15f ? "AT THE TOWER" : $"TOWER  {metres:F0} m";
            }

            enemies(pos);
            _place.text = _logger != null && !string.IsNullOrEmpty(_logger.CurrentSpace) ? _logger.CurrentSpace.ToUpperInvariant() : "MILITARY BASE";
        }

        /// <summary>World offset → minimap pixels, clamped to the frame edge. 'inside' is false when clamped.</summary>
        private Vector2 toMap(Vector3 worldOffset, out bool inside)
        {
            var scale = SizePixels / ViewMetres;
            var p = new Vector2(worldOffset.x, worldOffset.z) * scale;
            var limit = SizePixels / 2 - 8f;
            inside = Mathf.Abs(p.x) <= limit && Mathf.Abs(p.y) <= limit;
            if (!inside)
                p *= limit / Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
            return p;
        }

        private void enemies(Vector3 pos)
        {
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 1f;
                _soldiers.Clear();
                foreach (var actor in FindObjectsByType<BaseActor>(FindObjectsSortMode.None))
                    if (actor.Side == 0)
                        _soldiers.Add(actor);
            }

            var shown = 0;
            foreach (var drone in VantageDrone.All)
                if (drone != null && !drone.IsDead)
                    shown = dot(shown, drone.transform.position - pos);
            foreach (var soldier in _soldiers)
                if (soldier != null && soldier.IsAlive && soldier.isActiveAndEnabled)
                    shown = dot(shown, soldier.transform.position - pos);
            for (int i = shown; i < _dots.Count; i++)
                _dots[i].enabled = false;
        }

        private int dot(int index, Vector3 offset)
        {
            if (index >= MaxDots || new Vector2(offset.x, offset.z).magnitude > EnemyRange)
                return index;
            var p = toMap(offset, out var inside);
            if (!inside)
                return index;
            if (index >= _dots.Count)
                _dots.Add(image(rect("Enemy", _frame, Vector2.zero, new Vector2(8, 8)), Enemy, circle()));
            var d = _dots[index];
            d.enabled = true;
            d.rectTransform.anchoredPosition = p;
            // Enemies far above or below the player (other floors) are faded.
            d.color = new Color(Enemy.r, Enemy.g, Enemy.b, Mathf.Abs(offset.y) > 3f ? 0.35f : 1f);
            return index + 1;
        }

        #region UI

        private void build()
        {
            var canvasGo = new GameObject("VANTAGE Minimap Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Panel: below the playtest line in the top-left corner.
            var panel = (RectTransform)new GameObject("Minimap", typeof(RectTransform)).transform;
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
            panel.anchoredPosition = new Vector2(24, -36);
            panel.sizeDelta = new Vector2(SizePixels + 12, SizePixels + 52);
            panel.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.06f, 0.72f);

            _frame = rect("Map Frame", panel, Vector2.zero, new Vector2(SizePixels, SizePixels));
            _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 1);
            _frame.anchoredPosition = new Vector2(0, -6 - SizePixels / 2);
            _frame.gameObject.AddComponent<RectMask2D>();
            _map = _frame.gameObject.AddComponent<RawImage>();
            _map.texture = Map;
            _map.raycastTarget = false;
            _map.color = new Color(0.85f, 0.85f, 0.85f);

            // Accent edge, like the HUD panels.
            var accent = rect("Accent", panel, Vector2.zero, new Vector2(4, SizePixels + 52));
            accent.anchorMin = accent.anchorMax = new Vector2(0, 0.5f);
            accent.anchoredPosition = new Vector2(2, 0);
            image(accent, Accent, null);

            _tower = rect("Tower", _frame, Vector2.zero, new Vector2(14, 14));
            image(_tower, Accent, null).rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            _arrow = rect("Player", _frame, Vector2.zero, new Vector2(18, 18));
            image(_arrow, Color.white, arrow());

            var north = text(_frame, "N", 15, new Vector2(0, SizePixels / 2 - 12));
            north.color = Accent;

            _distance = text(panel, "Distance", 15, Vector2.zero);
            _distance.alignment = TextAnchor.MiddleLeft;
            var dr = _distance.rectTransform;
            dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0, 0);
            dr.anchoredPosition = new Vector2(12, 24);
            dr.sizeDelta = new Vector2(SizePixels - 6, 20);
            _place = text(panel, "Place", 12, Vector2.zero);
            _place.alignment = TextAnchor.MiddleLeft;
            _place.color = new Color(1, 1, 1, 0.6f);
            var pr = _place.rectTransform;
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0, 0);
            pr.anchoredPosition = new Vector2(12, 6);
            pr.sizeDelta = new Vector2(SizePixels - 6, 18);
        }

        private static RectTransform rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            r.SetParent(parent, false);
            r.anchoredPosition = position;
            r.sizeDelta = size;
            return r;
        }

        private static Image image(RectTransform r, Color color, Sprite sprite)
        {
            var i = r.gameObject.AddComponent<Image>();
            i.sprite = sprite;
            i.color = color;
            i.raycastTarget = false;
            return i;
        }

        private static Text text(RectTransform parent, string value, int size, Vector2 position)
        {
            var r = rect(value, parent, position, new Vector2(60, 20));
            var t = r.gameObject.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = value;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = r.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        private static Sprite _arrowSprite, _circleSprite;

        /// <summary>A white arrowhead pointing up (+y), drawn once.</summary>
        private static Sprite arrow()
        {
            if (_arrowSprite != null) return _arrowSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n;
                    var half = 0.45f * (1f - v);                       // narrows towards the tip at the top
                    var notch = v < 0.3f && Mathf.Abs(u) < (0.3f - v) * 1.2f; // swallow-tail at the bottom
                    px[y * n + x] = Mathf.Abs(u) <= half && !notch ? Color.white : Color.clear;
                }
            tex.SetPixels(px);
            tex.Apply();
            return _arrowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        private static Sprite circle()
        {
            if (_circleSprite != null) return _circleSprite;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01((1f - d) * 6f));
                }
            tex.SetPixels(px);
            tex.Apply();
            return _circleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        #endregion
    }
}
