using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vantage
{
    /// <summary>
    /// The robots' health bar on the HUD, bottom-centre (2026-11-23, the user's request: "so the player knows how much
    /// he needs to fight"). One bar for the robot the player is fighting: the L3 heavy robot once it fights, is hit or
    /// stands within its engage range on the player's floor; the boss from the end of its reveal until it dies. Its
    /// name, "Rifle only" when the pistol bounces off, a trailing chunk for the damage just dealt, and on the boss a tick
    /// at the helicopter's floor (its cannon never takes the boss below it: the rest is the player's). After a death the
    /// bar drains and fades out. Built by VantagePlayerHUD under its HUD root, so it hides with the HUD (cutscenes,
    /// pause, death, end screen).
    /// </summary>
    public class VantageBossBar : MonoBehaviour
    {
        public string RobotName = "Heavy robot";
        public string BossName = "Assault mech";
        [Tooltip("Width of the bar in canvas pixels.")]
        public float Width = 640f;
        [Tooltip("Seconds the bar stays after the robot dies (drained, then faded).")]
        public float AfterDeath = 1.6f;

        public static VantageBossBar Instance { get; private set; }
        /// <summary>The robot shown, or null.</summary>
        public VantageHeavyRobot Shown { get; private set; }
        public bool Visible => _group != null && _group.alpha > 0f;

        private RectTransform _root, _floorTick;
        private CanvasGroup _group;
        private Image _fill, _trail;
        private TMP_Text _name, _note;
        private float _trailValue = 1f, _deadAt = -1f, _nextPick;
        private VantageHeavyRobot _robot;
        private bool _wasBoss;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Builds the bar under the HUD root.</summary>
        public void Build(RectTransform hud)
        {
            var bc = new Vector2(0.5f, 0f);
            _root = VantageUI.Rect("Robot Health", hud, bc, new Vector2(0, 118), new Vector2(Width, 64));
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            VantageUI.Image(VantageUI.Rect("Shade", _root, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(Width + 360, 260)), new Color(0, 0, 0, 0.35f), VantageUI.Shade);
            _name = VantageUI.Text(_root, "Name", 30, TextAlignmentOptions.BottomLeft, new Vector2(0, 0.5f), new Vector2(0, 16), new Vector2(Width * 0.6f, 34));
            _note = VantageUI.Text(_root, "Note", 20, TextAlignmentOptions.BottomRight, new Vector2(1, 0.5f), new Vector2(0, 18), new Vector2(Width * 0.5f, 26));
            _note.color = VantageUI.Accent;
            _fill = VantageUI.Bar(_root, "Bar", new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(Width, 12), VantageUI.Danger, out _trail);
            _floorTick = VantageUI.Rect("Helicopter Floor", _fill.transform.parent, new Vector2(0.3f, 0.5f), Vector2.zero, new Vector2(3, 22));
            _floorTick.pivot = new Vector2(0.5f, 0.5f);
            VantageUI.Image(_floorTick, VantageUI.Muted);
            _floorTick.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_root == null) return;
            if (Time.time >= _nextPick) { _nextPick = Time.time + 0.25f; pick(); }
            var robot = _robot;
            var target = 0f;
            if (robot != null)
            {
                var max = Mathf.Max(1f, robot.MaxHealth);
                var fraction = robot.IsDead ? 0f : Mathf.Clamp01(robot.Health / max);
                if (robot.IsDead && _deadAt < 0f) _deadAt = Time.time;
                _fill.fillAmount = fraction;
                // The chunk just lost lingers, then slides down to the bar.
                _trailValue = fraction > _trailValue ? fraction : Mathf.MoveTowards(_trailValue, fraction, Time.deltaTime * 0.6f);
                _trail.fillAmount = _trailValue;
                target = _deadAt < 0f ? 1f : Mathf.Clamp01(1f - (Time.time - _deadAt - AfterDeath * 0.5f) / (AfterDeath * 0.5f));
                if (_deadAt >= 0f && Time.time - _deadAt > AfterDeath) { _robot = null; Shown = null; }
            }
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.deltaTime * 4f);
        }

        /// <summary>The robot to show: the boss once its reveal is over, else the L3 robot when it matters.</summary>
        private void pick()
        {
            if (_robot != null && _deadAt >= 0f) return;   // let a dead robot's bar drain first
            VantageHeavyRobot best = null;
            var levels = VantageTowerLevels.Instance;
            var player = VantageEvents.ActivePlayer();
            var robots = VantageHeavyRobot.All;
            for (int i = 0; i < robots.Count; i++)
            {
                var r = robots[i];
                if (r == null || r.IsDead) continue;
                if (r.IsBoss)
                {
                    if (!VantageBossReveal.Playing) { best = r; break; }
                    continue;
                }
                if (r.Engaging || r.Health < r.MaxHealth - 0.5f) { best = r; continue; }
                if (player == null || levels == null) continue;
                var near = (player.transform.position - r.transform.position).sqrMagnitude < r.EngageRange * r.EngageRange;
                if (near && levels.LevelAt(player.transform.position) == levels.LevelAt(r.transform.position)) best = r;
            }
            if (best == _robot) return;
            if (best == null)
            {
                // Out of the fight (walked away, another floor): fade it, keep nothing.
                _robot = null; Shown = null; return;
            }
            _robot = best;
            Shown = best;
            _deadAt = -1f;
            _trailValue = Mathf.Clamp01(best.Health / Mathf.Max(1f, best.MaxHealth));
            if (_wasBoss != best.IsBoss || string.IsNullOrEmpty(_name.text))
            {
                _wasBoss = best.IsBoss;
                _name.text = VantageUI.Caps(best.IsBoss ? BossName : RobotName);
                _note.text = best.RifleOnly ? VantageUI.Caps("Armoured: rifle only") : "";
            }
            var heli = VantageHelicopterSupport.Instance;
            var floor = best.IsBoss && heli != null ? heli.BossFloor : -1f;
            _floorTick.gameObject.SetActive(floor > 0f);
            if (floor > 0f) { _floorTick.anchorMin = _floorTick.anchorMax = new Vector2(floor, 0.5f); }
        }
    }
}
