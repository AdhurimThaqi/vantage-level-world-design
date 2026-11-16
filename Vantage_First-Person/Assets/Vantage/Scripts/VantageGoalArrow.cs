using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vantage
{
    /// <summary>
    /// The goal arrow on the HUD (lecturer's feedback, 2026-11-16: from outside, the red marker on the door didn't say
    /// whether the player should go there). One target at a time, the same as the guide sound's:
    ///   unarmed → the pistol; armed but still outside before the alarm → the tower entrance; then the step of
    ///   <see cref="VantageGoalSound"/> (stairs up, rifle, key, key slot, robot room, helipad).
    /// None during the lockdown (its goal is the soldiers, not a place) or while the floor has to be cleared.
    /// On screen it hangs over the goal with its name and distance; off screen it sits at the edge and points to it.
    /// Built by VantagePlayerHUD inside its HUD root, so it hides with the HUD (cutscenes, pause, death, end screen).
    /// </summary>
    public class VantageGoalArrow : MonoBehaviour
    {
        [Tooltip("Hidden this close to the goal (metres).")]
        public float HideWithin = 3.5f;
        [Tooltip("Distance from the screen edge for the off-screen arrow (canvas pixels).")]
        public float EdgeMargin = 90f;
        [Tooltip("Height above the goal's point the on-screen arrow hangs at (metres).")]
        public float Lift = 1.6f;
        public string PistolName = "Pistol";

        public static VantageGoalArrow Instance { get; private set; }
        /// <summary>Shown now (for the bot): there is a goal and the player is not on top of it.</summary>
        public bool Visible { get; private set; }
        /// <summary>The goal's name ("Pistol", "Tower entrance", "Key slot" …), or null.</summary>
        public string GoalName { get; private set; }
        /// <summary>The goal's world point.</summary>
        public Vector3 Goal { get; private set; }
        /// <summary>The arrow sits at the screen edge (the goal is off screen or behind the camera).</summary>
        public bool AtEdge { get; private set; }

        private RectTransform _root, _marker, _arrow;
        private TMP_Text _label;
        private Image _arrowImage;
        private Camera _camera;
        private VantageArsenal _arsenal;
        private Transform _pistol;
        private string _shownLabel;
        private int _shownMetres = -1;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Builds the arrow under the HUD root.</summary>
        public void Build(RectTransform hud)
        {
            _root = hud;
            _marker = VantageUI.Rect("Goal Arrow", hud, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 52));
            _arrow = VantageUI.Rect("Arrow", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
            _arrowImage = VantageUI.Image(_arrow, VantageUI.Accent, VantageUI.Arrow);
            _label = VantageUI.Text(_marker, "Goal", 26, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -46), new Vector2(360, 28));
            _label.color = VantageUI.Accent;
            _marker.gameObject.SetActive(false);
        }

        private void Start()
        {
            _arsenal = GetComponent<VantageArsenal>();
            foreach (var pickup in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                if (pickup.WeaponName == PistolName) _pistol = pickup.transform;
        }

        private void LateUpdate()
        {
            if (_marker == null) return;
            var name = pick(out var goal);
            var player = VantageEvents.ActivePlayer();
            var distance = player != null ? Vector3.Distance(player.transform.position, goal) : 0f;
            Visible = name != null && player != null && distance > HideWithin;
            GoalName = name;
            Goal = goal;
            if (_marker.gameObject.activeSelf != Visible) _marker.gameObject.SetActive(Visible);
            if (!Visible) { AtEdge = false; return; }
            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            if (_camera == null) return;
            place(goal + Vector3.up * Lift);

            var metres = Mathf.RoundToInt(distance);
            if (!ReferenceEquals(name, _shownLabel) || metres != _shownMetres)
            {
                _shownLabel = name;
                _shownMetres = metres;
                _label.text = VantageUI.Caps($"{name} {metres} m");
            }
        }

        /// <summary>The current goal (the guide sound's ladder plus the two outside steps), or null.</summary>
        private string pick(out Vector3 goal)
        {
            goal = Vector3.zero;
            var levels = VantageTowerLevels.Instance;
            if (levels == null || levels.Completed) return null;
            if (_arsenal != null && !_arsenal.IsUnlocked(PistolName) && _pistol != null && _pistol.gameObject.activeInHierarchy)
            {
                goal = _pistol.position;
                return "Pistol";
            }
            var alarm = VantageAlarm.Instance;
            var player = VantageEvents.ActivePlayer();
            var intro = VantageIntro.Instance;
            if (levels.Current == 0 && (alarm == null || alarm.Raised == 0) && player != null && levels.LevelAt(player.transform.position) < 0
                && intro != null && intro.EntrancePoint != Vector3.zero)
            {
                goal = intro.EntrancePoint;
                return "Tower entrance";
            }
            var guide = VantageGoalSound.Instance;
            if (guide == null || guide.Anchor == null) return null;
            var label = labelOf(guide.Current);
            if (label != null) goal = guide.Anchor.position;
            return label;
        }

        private static string labelOf(int step)
        {
            switch (step)
            {
                case VantageGoalSound.L1Cleared:
                case VantageGoalSound.L2GateOpen:
                case VantageGoalSound.L3Cleared: return "Stairs up";
                case VantageGoalSound.FindRifle: return "Rifle";
                case VantageGoalSound.FindKey: return "Key";
                case VantageGoalSound.UseKey: return "Key slot";
                case VantageGoalSound.RobotRoomOpen: return "Breach";
                case VantageGoalSound.ReachPad: return "Helipad";
                default: return null;   // the lockdown: the soldiers are the goal
            }
        }

        /// <summary>Over the goal when it is on screen; otherwise at the edge, pointing towards it.</summary>
        private void place(Vector3 world)
        {
            var screen = _camera.WorldToScreenPoint(world);
            var behind = screen.z < 0f;
            if (behind) screen = new Vector3(Screen.width - screen.x, Screen.height - screen.y, 0f);   // mirrored: the side it is on
            var size = _root.rect.size;
            var scale = size.x / Mathf.Max(1f, Screen.width);
            var local = new Vector2((screen.x - Screen.width * 0.5f) * scale, (screen.y - Screen.height * 0.5f) * scale);
            var half = size * 0.5f - new Vector2(EdgeMargin, EdgeMargin);
            AtEdge = behind || Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y;
            if (AtEdge)
            {
                if (behind && local.sqrMagnitude < 1f) local = Vector2.down;
                var k = Mathf.Min(half.x / Mathf.Max(0.01f, Mathf.Abs(local.x)), half.y / Mathf.Max(0.01f, Mathf.Abs(local.y)));
                local *= k;   // onto the margin rectangle, along the line from the centre
                _arrow.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg - 90f);
            }
            else
                _arrow.localEulerAngles = new Vector3(0, 0, 180f);   // hanging over the goal, pointing down at it
            _marker.anchoredPosition = local;
            var pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            _arrowImage.color = new Color(VantageUI.Accent.r, VantageUI.Accent.g, VantageUI.Accent.b, AtEdge ? 1f : pulse);
        }
    }
}
