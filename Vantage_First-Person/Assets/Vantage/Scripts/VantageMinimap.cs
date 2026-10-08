using System.Collections.Generic;
using CoverShooter;
using TMPro;
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
        private static Color Accent => VantageUI.Accent;
        private static Color Enemy => VantageUI.Danger;

        private const int MaxDots = 24;
        private float _otherFloor = 3f;

        private RectTransform _frame;
        private RawImage _map;
        private RectTransform _arrow, _tower;
        private TMP_Text _distance, _place;
        private readonly List<Image> _dots = new List<Image>();
        private int _shownMetres = -1, _shownPlace = -2;

        private void Start()
        {
            if (Map == null || WorldArea.width <= 0)
            {
                enabled = false;
                return;
            }
            build();
        }

        private void LateUpdate()
        {
            var player = VantageEvents.ActivePlayer();
            if (player == null)
                return; // dead: the map holds its last view under the death screen

            var pos = player.transform.position;
            var view = ViewMetres / WorldArea.width;
            var u = (pos.x - WorldArea.x) / WorldArea.width;
            var v = (pos.z - WorldArea.y) / WorldArea.height;
            _map.uvRect = new Rect(u - view / 2, v - view / 2, view, view * WorldArea.width / WorldArea.height);

            _arrow.localEulerAngles = new Vector3(0, 0, -player.transform.eulerAngles.y);

            if (Tower != null)
            {
                var offset = toMap(Tower.position - pos, out var inside);
                _tower.anchoredPosition = offset;
                _tower.localScale = Vector3.one * (inside ? 1f : 0.8f);
                var metres = Mathf.RoundToInt(Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(Tower.position.x, Tower.position.z)));
                if (metres != _shownMetres)
                {
                    _shownMetres = metres;
                    _distance.text = metres < 15 ? VantageUI.Caps("At the tower") : VantageUI.Caps($"Tower {metres} m");
                }
            }

            enemies(pos);
            place(pos);
        }

        /// <summary>Where the player is: the base outside, or the tower's floor (only rebuilt when it changes).</summary>
        private void place(Vector3 pos)
        {
            var levels = VantageTowerLevels.Instance;
            var storey = levels == null || levels.LevelAt(pos) < 0 ? -1 : levels.StoreyAt(levels.Tower.InverseTransformPoint(pos).y);
            if (storey == _shownPlace) return;
            _shownPlace = storey;
            _place.text = VantageUI.Caps(storey < 0 ? "Military base" : "Tower · " + levels.StoreyName(storey));
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
            // While the level's last enemies hunt the player, they are shown at any distance (pinned to the edge).
            var levels = VantageTowerLevels.Instance;
            var showAll = levels != null && levels.Hunting;
            _otherFloor = levels != null ? levels.StoreyHeight * 0.75f : 3f;
            var shown = 0;
            for (int i = 0; i < VantageDrone.All.Count; i++)
            {
                var drone = VantageDrone.All[i];
                if (drone != null && !drone.IsDead)
                    shown = dot(shown, drone.transform.position - pos, showAll);
            }
            // Soldiers are the template's actors on the enemy side (the template keeps the list; no scene search).
            for (int i = 0; i < Actors.Count; i++)
            {
                var actor = Actors.Get(i);
                if (actor != null && actor.Side == VantageTowerLevels.EnemySide && actor.IsAlive && actor.isActiveAndEnabled)
                    shown = dot(shown, actor.transform.position - pos, showAll);
            }
            for (int i = shown; i < _dots.Count; i++)
                _dots[i].enabled = false;
        }

        private int dot(int index, Vector3 offset, bool showAll)
        {
            if (index >= MaxDots || (!showAll && new Vector2(offset.x, offset.z).sqrMagnitude > EnemyRange * EnemyRange))
                return index;
            var p = toMap(offset, out var inside);
            if (!inside && !showAll)
                return index;
            if (index >= _dots.Count)
                _dots.Add(VantageUI.Image(VantageUI.Rect("Enemy", _frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 8)), Enemy, VantageUI.Circle));
            var d = _dots[index];
            d.enabled = true;
            d.rectTransform.anchoredPosition = p;
            // Enemies far above or below the player (other floors) are faded.
            d.color = new Color(Enemy.r, Enemy.g, Enemy.b, Mathf.Abs(offset.y) > _otherFloor ? 0.35f : 1f);
            return index + 1;
        }

        #region UI

        private void build()
        {
            var canvas = VantageUI.Canvas("VANTAGE Minimap Canvas", 40, transform);

            // Top-left corner: the map in a thin frame, distance and place below it.
            var panel = VantageUI.Rect("Minimap", canvas, new Vector2(0, 1), new Vector2(40, -40), new Vector2(SizePixels + 4, SizePixels + 4));
            VantageUI.Image(panel, new Color(1f, 1f, 1f, 0.25f));
            _frame = VantageUI.Rect("Map Frame", panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SizePixels, SizePixels));
            _frame.gameObject.AddComponent<RectMask2D>();
            _map = _frame.gameObject.AddComponent<RawImage>();
            _map.texture = Map;
            _map.raycastTarget = false;
            _map.color = new Color(0.8f, 0.8f, 0.8f);

            _tower = VantageUI.Rect("Tower", _frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 14));
            VantageUI.Image(_tower, Accent, VantageUI.Pixel).rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            _arrow = VantageUI.Rect("Player", _frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
            VantageUI.Image(_arrow, Color.white, arrow());
            VantageUI.Text(_frame, "N", 20, TextAlignmentOptions.Top, new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(30, 24)).color = Accent;

            _distance = VantageUI.Text(canvas, "Distance", 24, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(42, -SizePixels - 52), new Vector2(SizePixels, 28));
            _place = VantageUI.Text(canvas, "Place", 18, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(42, -SizePixels - 78), new Vector2(SizePixels, 24));
            _place.color = VantageUI.Muted;
        }

        private static Sprite _arrowSprite;

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

        #endregion
    }
}
