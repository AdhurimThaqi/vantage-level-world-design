using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vantage
{
    /// <summary>
    /// The intro cutscene, once at the start of a run (not after a death), and the mission briefing at the same time:
    ///   1. high over the base in on the tower ("VANTAGE");
    ///   2. a deserter walks out past the tower plot; the slab sentry fires a warning shot, he runs, the next shot
    ///      kills him (the warning ring shown, not told: "Rule: stay at the tower");
    ///   3. the climb: the camera circles up the outside of the tower, one stop per level at its floor height with
    ///      that level's task (level 3 with the slab sentry in frame), and ends on the helipad (the boss, the way out);
    ///   4. down to the pistol on the ground ("First: grab the pistol"), then into the player's own camera; the HUD
    ///      then shows that objective once more as a banner.
    /// From the climb on the camera moves around the tower, never through it (angle, radius and height eased
    /// separately, the radius kept outside the tower's footprint). Every position comes from the scene (the entrance,
    /// the tower's area and floor heights, the ring's plot, the sentries, the helipad, the pistol), so a re-export
    /// moves the shots with the tower. Any key or mouse button skips it after half a second.
    /// Letterbox bars and captions; the HUD and minimap hide while VantageCinematic has the camera.
    /// </summary>
    public class VantageIntro : MonoBehaviour
    {
        public Transform Helipad;
        [Tooltip("Ground point of the main entrance and its outward direction (set by Setup).")]
        public Vector3 EntrancePoint, EntranceFacing = Vector3.back;
        [Header("Sounds (empty: synthesized)")]
        public AudioClip Music;
        public AudioClip Whoosh;

        public static VantageIntro Instance { get; private set; }
        public bool Playing { get; private set; }
        /// <summary>Seconds since the intro started.</summary>
        public float Elapsed => Playing ? Time.time - _startTime : _endedAt;
        /// <summary>The soldier walking out in shot 2 (null when there was no open ground in the sentry's view).</summary>
        public VantageDeserter Deserter { get; private set; }
        /// <summary>The sentry's warning shot at the deserter has been fired.</summary>
        public bool WarningShotFired { get; private set; }
        /// <summary>Raised when it ends: skipped or not, and at which second.</summary>
        public static event Action<bool, float> Finished;

        // Shot ends (seconds) and the deserter's beats. The climb has one beat per stop (three floors, the helipad).
        public const float Shot1 = 3.5f, ShotDeserter = 9.5f, Beat = 2.6f;
        public const float ShotClimb = ShotDeserter + 4 * Beat, ShotPistol = ShotClimb + 2.6f, Length = ShotPistol + 1.4f;
        public const float WalkAt = 2.5f, AimAt = 5.4f, WarnAt = 6f, KillAt = 8f;
        /// <summary>When the camera holds at climb stop i (0-2 the floors, 3 the helipad).</summary>
        public static float StopAt(int i) => ShotDeserter + i * Beat + 1.7f;
        private const float SkipAfter = 0.5f, GlintTime = 0.4f;
        private const string SlabSentry = "SENTRY_Slab";
        private static readonly string[] Kickers =
        {
            "Military training base", "Rule", "Level 1: Ground floor", "Level 2: Armory", "Level 3: Destroyed floor",
            "Finale: Helipad", "First",
        };
        private static readonly string[] Captions =
        {
            "VANTAGE", "Stay at the tower: the sentries shoot deserters", "A trap locks you in: clear the floor",
            "Find the rifle and the key", "Only the rifle hurts the machine", "Kill the boss: the helicopter is your way out",
            "Grab the pistol",
        };

        private Camera _camera;
        private Vector3 _endPosition;
        private Quaternion _endRotation;
        private float _startTime, _endedAt;
        private Vector3 _towerCentre, _p0, _p1;
        private Vector3 _heading, _across, _fallen;
        private float _safeRadius;
        private bool _fromMenu;
        private const float Margin = 4f;
        private Transform _pistol;
        private Vector3 _pad;
        private bool _walking, _killed;
        private RectTransform _canvas, _top, _bottom;
        private Image _cover;
        private TMP_Text _kicker, _caption;
        private int _shownCaption = -1;
        private AudioSource _music;
        private static AudioClip _swoosh;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private IEnumerator Start()
        {
            VantageRun.Begin();
            if (VantageRun.Checkpoint != 0 || VantageRun.IntroPlayed) yield break;
            VantageRun.IntroPlayed = true;
            // The title screen comes first, once per session; it covers the first frame itself.
            var menu = VantageRun.MenuShown ? null : GetComponent<VantageMainMenu>();
            if (menu == null)
            {
                Playing = true;
                _startTime = Time.time;
                buildOverlay();   // black at first: covers the one frame the gameplay camera shows
            }

            // The player's own camera places itself in its first LateUpdate: that pose is where the intro ends.
            // (One frame, not WaitForEndOfFrame: that never resumes when nothing renders, e.g. in batch mode.)
            yield return null;
            _camera = Camera.main;
            if (_camera == null) { finish(false); yield break; }
            _endPosition = _camera.transform.position;
            _endRotation = _camera.transform.rotation;
            VantageCinematic.Begin();

            var levels = VantageTowerLevels.Instance;
            _towerCentre = levels != null ? levels.TowerArea.center : EntrancePoint;
            var facing = EntranceFacing; facing.y = 0;
            facing = facing.sqrMagnitude > 0.01f ? facing.normalized : Vector3.back;
            _p0 = EntrancePoint + facing * 110f + Vector3.up * 40f;
            _p1 = EntrancePoint + facing * 55f + Vector3.up * 25f;
            if (menu != null)
            {
                VantageRun.MenuShown = true;
                yield return menu.Run(_camera, _towerCentre, levels != null ? levels.TowerArea : new Bounds(_towerCentre, Vector3.one * 20f), facing);
                _p0 = _camera.transform.position;   // the first shot flies on from where the menu left the camera
                VantageRun.StartTime = Time.realtimeSinceStartup;   // the run's clock starts at Play
                Playing = true;
                _startTime = Time.time;
                buildOverlay();
                _cover.color = Color.clear;
                _fromMenu = true;
            }
            // Outside every wall: the far corner of the tower's footprint, plus room.
            var area = levels != null ? levels.TowerArea : new Bounds(_towerCentre, Vector3.one * 20f);
            _safeRadius = 0f;
            foreach (var x in new[] { area.min.x, area.max.x })
                foreach (var z in new[] { area.min.z, area.max.z })
                    _safeRadius = Mathf.Max(_safeRadius, new Vector2(x - _towerCentre.x, z - _towerCentre.z).magnitude);
            _safeRadius += Margin;
            foreach (var pickup in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                if (pickup.WeaponName == "Pistol") _pistol = pickup.transform;
            _pad = padTop();
            placeDeserter();
            playMusic();
        }

        private void Update()
        {
            if (!Playing || _camera == null) return;
            var t = Time.time - _startTime;
            if (t >= SkipAfter && (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))) { finish(true); return; }
            if (t >= Length) { finish(false); return; }
        }

        private void LateUpdate()
        {
            if (!Playing || _camera == null) return;
            var t = Time.time - _startTime;
            runDeserter(t);
            pose(t, out var position, out var rotation);
            _camera.transform.SetPositionAndRotation(position, rotation);
            updateOverlay(t);
        }

        private void pose(float t, out Vector3 position, out Quaternion rotation)
        {
            var sentry = sentryPoint(SlabSentry, 0);
            var sentryAt = framing(sentry, 9f, 3f);
            deserterView(sentry, sentryAt, out var watchAt, out var watch);
            if (t < Shot1)
            {
                position = Vector3.Lerp(_p0, _p1, Mathf.SmoothStep(0, 1, t / Shot1));
                rotation = Quaternion.LookRotation(_towerCentre - position);
                return;
            }
            if (t < ShotDeserter)
            {
                var k = Mathf.SmoothStep(0, 1, (t - Shot1) / 1.6f);
                position = Vector3.Lerp(_p1, watchAt, k);
                rotation = Quaternion.Slerp(Quaternion.LookRotation(_towerCentre - _p1), Quaternion.LookRotation(watch - watchAt), k);
                return;
            }
            if (t >= ShotPistol)
            {
                // From the pistol into the player's own camera (both in front of the entrance).
                pistolView(out var from, out var at);
                var k = Mathf.SmoothStep(0, 1, (t - ShotPistol) / (Length - ShotPistol));
                position = Vector3.Lerp(from, _endPosition, k);
                rotation = Quaternion.Slerp(Quaternion.LookRotation(at - from), _endRotation, k);
                return;
            }

            // The climb and the pistol: eased from stop to stop around the tower.
            var stops = climbStops(sentry, watchAt, watch);
            var i = 0;
            while (i < stops.Length - 2 && t >= stops[i + 1].time) i++;
            var a = stops[i];
            var b = stops[i + 1];
            var s = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - a.time) / (b.time - a.time)));
            position = around(a.position, b.position, s);
            rotation = Quaternion.LookRotation(Vector3.Lerp(a.look, b.look, s) - position);
        }

        /// <summary>
        /// Where the camera holds, in order: the end of the deserter shot, each floor from outside at its own height
        /// (level 1 on the entrance side, level 3 on the slab sentry's side with the sentry in frame, level 2 between),
        /// the helipad from above, and the pistol.
        /// </summary>
        private (float time, Vector3 position, Vector3 look)[] climbStops(Vector3 sentry, Vector3 watchAt, Vector3 watch)
        {
            var levels = VantageTowerLevels.Instance;
            var pad = _pad;
            float floor(int n) => levels != null && levels.Levels.Count > n ? levels.Levels[n].FloorY : _towerCentre.y + 4f * n;
            var entrance = angleOf(_towerCentre + EntranceFacing);
            var slab = angleOf(sentry);
            var middle = Mathf.LerpAngle(entrance, slab, 0.5f);
            var padRadius = Mathf.Max(_safeRadius, flat(pad - _towerCentre).magnitude + 10f);
            var centre = new Vector3(_towerCentre.x, 0, _towerCentre.z);
            pistolView(out var pistolFrom, out var pistolAt);
            return new[]
            {
                (ShotDeserter, watchAt, watch),
                (StopAt(0), clear(entrance, _safeRadius, floor(0) + 2.2f), centre + Vector3.up * (floor(0) + 1.6f)),
                (StopAt(1), clear(middle, _safeRadius, floor(1) + 2.2f), centre + Vector3.up * (floor(1) + 1.6f)),
                (StopAt(2), clear(slab, _safeRadius, floor(2) + 2.8f), Vector3.Lerp(centre + Vector3.up * (floor(2) + 1.6f), sentry, 0.5f)),
                (StopAt(3), clear(angleOf(pad), padRadius, pad.y + 7f), pad),
                (ShotClimb + 1.8f, pistolFrom, pistolAt),
                (ShotPistol, pistolFrom, pistolAt),
            };
        }

        /// <summary>The pistol from just outside it, looking back past it at the entrance (the entrance if it is gone).</summary>
        private void pistolView(out Vector3 from, out Vector3 at)
        {
            var p = _pistol != null && _pistol.gameObject.activeInHierarchy ? _pistol.position : EntrancePoint;
            var outward = flat(p - _towerCentre);
            outward = outward.sqrMagnitude > 0.01f ? outward.normalized : EntranceFacing;
            from = p + outward * 4.5f + Vector3.up * 2.4f;
            at = p + Vector3.up * 0.3f;
        }

        /// <summary>
        /// From a to b around the tower: angle, radius and height eased separately, and the radius pushed out to the
        /// safe radius in between when an end is closer in (the pistol), so the camera never passes through the tower.
        /// </summary>
        private Vector3 around(Vector3 a, Vector3 b, float s)
        {
            var ra = flat(a - _towerCentre).magnitude;
            var rb = flat(b - _towerCentre).magnitude;
            var radius = Mathf.Lerp(ra, rb, s) + Mathf.Max(0f, _safeRadius - Mathf.Min(ra, rb)) * Mathf.Sin(Mathf.PI * s);
            return orbit(Mathf.LerpAngle(angleOf(a), angleOf(b), s), radius, Mathf.Lerp(a.y, b.y, s));
        }

        /// <summary>
        /// A stop around the tower, pulled in toward it when something stands between (the base fence, a pole):
        /// searched outward from just past the tower's corners, so the camera always ends up outside the tower.
        /// </summary>
        private Vector3 clear(float angle, float radius, float height)
        {
            var near = orbit(angle, _safeRadius - Margin + 1f, height);
            var far = orbit(angle, radius, height);
            var to = far - near;
            if (Physics.SphereCast(near, 0.4f, to.normalized, out var hit, to.magnitude, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                return near + to.normalized * Mathf.Max(0f, hit.distance - 0.6f);
            return far;
        }

        /// <summary>The middle of the helipad's top surface (its pivot is not on the pad).</summary>
        private Vector3 padTop()
        {
            if (Helipad == null) return _towerCentre + Vector3.up * 12f;
            var renderers = Helipad.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return Helipad.position;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        private Vector3 orbit(float angle, float radius, float height)
        {
            var dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            return new Vector3(_towerCentre.x, height, _towerCentre.z) + dir * radius;
        }

        private float angleOf(Vector3 point)
        {
            var d = flat(point - _towerCentre);
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        private static Vector3 flat(Vector3 v) { v.y = 0; return v; }

        /// <summary>Jumps to the player's camera and ends the intro (no-op when it isn't playing).</summary>
        public void Skip()
        {
            if (Playing) finish(true);
        }

        private void finish(bool skipped)
        {
            if (!Playing) return;
            _endedAt = Time.time - _startTime;
            Playing = false;
            // Skipped early: he still lies out there, the ring's first victim.
            var slab = slabSentry();
            if (slab != null) slab.Glint(0f);
            if (Deserter != null) Deserter.Kill(slab != null ? slab.gameObject : null);
            if (_camera != null) _camera.transform.SetPositionAndRotation(_endPosition, _endRotation);
            VantageCinematic.End();
            if (_music != null) StartCoroutine(fadeOut(_music, 1.5f));
            var whoosh = Whoosh != null ? Whoosh : swoosh();
            if (whoosh != null && _camera != null) AudioSource.PlayClipAtPoint(whoosh, _camera.transform.position, 0.6f);
            if (_canvas != null) Destroy(_canvas.gameObject);
            Finished?.Invoke(skipped, _endedAt);
        }

        #region The deserter

        /// <summary>
        /// The deserter walks out from the tower plot, away from the slab sentry's side of the tower, over open
        /// ground in the sentry's view: from just past the free zone to just short of the kill zone (the ring's own
        /// distances). Directions are tried around "straight out" until one is clear.
        /// </summary>
        private void placeDeserter()
        {
            var levels = VantageTowerLevels.Instance;
            var ring = VantageBoundary.Instance;
            var sentry = slabSentry();
            if (levels == null || levels.SoldierPrefab == null || ring == null || sentry == null) return;
            var eye = VantagePhysics.Eye(sentry);
            var head = Vector3.up * levels.SoldierEyeHeight;
            var centre = new Vector3(ring.Plot.center.x, eye.y, ring.Plot.center.y);
            var outward = sentry.transform.position - centre; outward.y = 0;
            if (outward.sqrMagnitude < 0.01f) outward = EntranceFacing;
            outward.Normalize();
            foreach (var angle in new[] { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f })
            {
                var dir = Quaternion.Euler(0, angle, 0) * outward;
                if (!ground(along(ring, centre, dir, ring.FreeDistance + 4f), out var from) || !ground(along(ring, centre, dir, ring.WarningDistance - 2f), out var to)) continue;
                var flat = to - from; flat.y = 0;
                if (Mathf.Abs(to.y - from.y) > flat.magnitude * 0.35f) continue;
                // A clear walk (knees and head) and both ends in the sentry's view.
                if (Physics.Linecast(from + head * 0.3f, to + head * 0.3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)
                    || Physics.Linecast(from + head, to + head, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)
                    || Physics.Linecast(eye, from + head, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)
                    || Physics.Linecast(eye, to + head, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)) continue;
                _heading = flat.normalized;
                _across = Vector3.Cross(Vector3.up, _heading);
                Deserter = VantageDeserter.Spawn(levels.SoldierPrefab, from + Vector3.up * 0.05f, _heading);
                Debug.Log($"[Vantage] Intro: deserter walks from {from:F1} to {to:F1} ({ring.OutsidePlot(from):F0}–{ring.OutsidePlot(to):F0} m past the plot).");
                return;
            }
            Debug.Log("[Vantage] Intro: no open ground in the slab sentry's view; the intro runs without the deserter.");
        }

        /// <summary>The point straight out from the plot's centre that is `distance` past the plot.</summary>
        private static Vector3 along(VantageBoundary ring, Vector3 centre, Vector3 dir, float distance)
        {
            var p = centre;
            for (int i = 0; i < 800 && ring.OutsidePlot(p) < distance; i++) p += dir * 0.5f;
            return p;
        }

        private static bool ground(Vector3 above, out Vector3 point)
        {
            var hit = Physics.Raycast(above + Vector3.up * 60f, Vector3.down, out var h, 200f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore);
            point = hit ? h.point : above;
            return hit;
        }

        /// <summary>He walks, the sentry aims and glints, a warning shot (he runs), a glint, the shot that kills him.</summary>
        private void runDeserter(float t)
        {
            if (Deserter == null) return;
            if (!_walking && t >= WalkAt) { _walking = true; Deserter.Move(0.5f); }
            var sentry = slabSentry();
            if (sentry == null || sentry.IsDead || _killed) return;
            if (t >= AimAt) sentry.HoldAim(Deserter.AimPoint, 1f);
            sentry.Glint(Mathf.Max(glint(t, WarnAt), glint(t, KillAt)));
            if (!WarningShotFired && t >= WarnAt)
            {
                WarningShotFired = true;
                var miss = Deserter.transform.position + _heading * 1.8f + _across * 0.7f;
                if (ground(miss, out var dirt)) miss = dirt;
                sentry.ScriptedShot(miss, true);
                Deserter.Move(1f);
            }
            if (t >= KillAt)
            {
                _killed = true;
                sentry.ScriptedShot(Deserter.AimPoint, false);
                Deserter.Kill(sentry.gameObject);
            }
        }

        private static float glint(float t, float shot) => t < shot - GlintTime || t >= shot ? 0f : (t - (shot - GlintTime)) / GlintTime;

        /// <summary>
        /// Shot 2's camera: ahead of the deserter and to one side, low, looking back past him at the tower and the
        /// sentry above; it stops following where he falls. Without a deserter it eases toward the sentry instead.
        /// </summary>
        private void deserterView(Vector3 sentry, Vector3 sentryAt, out Vector3 position, out Vector3 look)
        {
            if (Deserter == null)
            {
                position = Vector3.Lerp(_p1, sentryAt, 0.35f);
                look = sentry;
                return;
            }
            if (!_killed) _fallen = Deserter.transform.position;
            var chest = Deserter.AimPoint - Deserter.transform.position;
            position = _fallen + _heading * 6.5f + _across * 2.2f + chest * 1.2f;
            if (ground(position, out var floor)) position.y = Mathf.Max(position.y, floor.y + chest.y);
            look = Vector3.Lerp(_fallen + chest, sentry, 0.35f);
        }

        #endregion

        private VantageSentry slabSentry()
        {
            foreach (var s in VantageSentries.All)
                if (s != null && s.name == SlabSentry) return s;
            return null;
        }

        /// <summary>A sentry's chest, or the middle of its route when it is missing (dead earlier in the run).</summary>
        private Vector3 sentryPoint(string name, int fallbackRoute)
        {
            foreach (var s in VantageSentries.All)
                if (s != null && s.name == name) return s.transform.position + Vector3.up * 1.4f;
            var spawner = FindAnyObjectByType<VantageSentries>();
            if (spawner != null && spawner.Routes.Count > fallbackRoute && spawner.Routes[fallbackRoute].Points.Count > 0)
            {
                var pts = spawner.Routes[fallbackRoute].Points;
                return pts[pts.Count / 2] + Vector3.up * 1.4f;
            }
            return _towerCentre;
        }

        /// <summary>A camera spot looking at `target` from outside the tower: `distance` out and `height` up.</summary>
        private Vector3 framing(Vector3 target, float distance, float height)
        {
            var outward = target - _towerCentre; outward.y = 0;
            if (outward.sqrMagnitude < 0.01f) outward = EntranceFacing;
            return target + outward.normalized * distance + Vector3.up * height;
        }

        #region Overlay and sound

        private void buildOverlay()
        {
            _canvas = VantageUI.Canvas("VANTAGE Intro", 60);
            _cover = VantageUI.Image(VantageUI.Stretch("Fade", _canvas), Color.black);
            _top = VantageUI.Rect("Letterbox Top", _canvas, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000, 0));
            _bottom = VantageUI.Rect("Letterbox Bottom", _canvas, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000, 0));
            _top.pivot = new Vector2(0.5f, 1f);
            _bottom.pivot = new Vector2(0.5f, 0f);
            VantageUI.Image(_top, Color.black);
            VantageUI.Image(_bottom, Color.black);
            _kicker = VantageUI.Text(_canvas, "Kicker", 22, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0, 112), new Vector2(1400, 26));
            _caption = VantageUI.Text(_canvas, "Caption", 40, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0, 52), new Vector2(1400, 60));
            _kicker.color = _caption.color = Color.clear;
        }

        /// <summary>Which caption shows when: (start, end) per caption, inside its shot.</summary>
        private static (float start, float end) captionTime(int shot) => shot switch
        {
            0 => (0f, Shot1),
            1 => (Shot1 + 1.2f, ShotDeserter),
            6 => (ShotClimb, Length - 0.3f),
            _ => (ShotDeserter + (shot - 2) * Beat + 0.5f, ShotDeserter + (shot - 1) * Beat + 0.5f),   // a climb stop, as the camera arrives
        };

        private void updateOverlay(float t)
        {
            if (_canvas == null) return;
            // Bars slide in over the first half second and out over the last.
            var bars = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((Length - t) / 0.6f);
            var h = 130f * bars;
            _top.sizeDelta = new Vector2(4000, h);
            _bottom.sizeDelta = new Vector2(4000, h);
            _cover.color = _fromMenu ? Color.clear : new Color(0, 0, 0, 1f - Mathf.Clamp01(t / 0.8f));

            var shot = -1;
            for (int i = 0; i < Captions.Length; i++)
            {
                var (s, e) = captionTime(i);
                if (t >= s && t < e) shot = i;
            }
            if (shot != _shownCaption)
            {
                _shownCaption = shot;
                _caption.text = shot < 0 ? "" : VantageUI.Caps(Captions[shot]);
                _kicker.text = shot < 0 ? "" : VantageUI.Caps(Kickers[shot]);
                _caption.fontSize = shot == 0 ? 72 : 40;
            }
            var a = 0f;
            if (shot >= 0)
            {
                var (start, end) = captionTime(shot);
                a = Mathf.Clamp01((t - start - 0.3f) / 0.5f) * Mathf.Clamp01((end - t) / 0.4f);
            }
            _caption.color = shot == 0 ? new Color(1, 1, 1, a) : new Color(VantageUI.Accent.r, VantageUI.Accent.g, VantageUI.Accent.b, a);
            _kicker.color = new Color(1, 1, 1, a * 0.75f);
        }

        private void playMusic()
        {
            _music = gameObject.AddComponent<AudioSource>();
            _music.spatialBlend = 0f;
            _music.loop = Music == null;
            _music.clip = Music != null ? Music : VantageProceduralAudio.Create(VantageProceduralAudio.Kind.Wind, 3);
            _music.volume = Music != null ? 0.8f : 0.5f;
            _music.Play();
        }

        private static IEnumerator fadeOut(AudioSource source, float seconds)
        {
            var from = source.volume;
            for (float t = 0; t < seconds && source != null; t += Time.deltaTime)
            {
                source.volume = from * (1f - t / seconds);
                yield return null;
            }
            if (source != null) source.Stop();
        }

        /// <summary>A short rising noise sweep for the hand-back (made once).</summary>
        private static AudioClip swoosh()
        {
            if (_swoosh != null) return _swoosh;
            const int rate = 44100;
            var samples = new float[rate / 2];
            var random = new System.Random(5);
            float low = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                var t = (float)i / samples.Length;
                var cut = Mathf.Lerp(0.02f, 0.35f, t);
                low += cut * ((float)(random.NextDouble() * 2 - 1) - low);
                samples[i] = low * Mathf.Sin(t * Mathf.PI) * 0.8f;
            }
            _swoosh = AudioClip.Create("Intro Whoosh", samples.Length, 1, rate, false);
            _swoosh.SetData(samples, 0);
            return _swoosh;
        }

        #endregion
    }
}
