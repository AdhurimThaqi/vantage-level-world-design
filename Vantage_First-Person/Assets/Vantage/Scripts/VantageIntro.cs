using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vantage
{
    /// <summary>
    /// The intro cutscene, once at the start of a run (not after a death): high over the base in on the tower, up to
    /// the sentry on the broken slab, across to the helipad (the way out), then down into the player's own camera.
    /// It shows the goal and the threat before any HUD text does. Every position comes from the scene (the
    /// entrance, the tower's area, the sentries, the helipad), so a re-export moves the shots with the tower.
    /// Any key or mouse button skips it after half a second. Letterbox bars and captions; the HUD and minimap hide
    /// while VantageCinematic has the camera.
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
        /// <summary>Raised when it ends: skipped or not, and at which second.</summary>
        public static event Action<bool, float> Finished;

        private const float Shot1 = 3.5f, Shot2 = 7f, Shot3 = 9.5f, Length = 13f, SkipAfter = 0.5f;
        private static readonly string[] Captions = { "VANTAGE", "Sentries watch the ground", "Your way out: the helipad" };

        private Camera _camera;
        private Vector3 _endPosition;
        private Quaternion _endRotation;
        private float _startTime, _endedAt;
        private Vector3 _towerCentre, _p0, _p1;
        private RectTransform _canvas, _top, _bottom;
        private Image _cover;
        private TMP_Text _caption;
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
            Playing = true;
            _startTime = Time.time;
            buildOverlay();   // black at first: covers the one frame the gameplay camera shows

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
            Vector3 position;
            Quaternion rotation;
            if (t < Shot1)
            {
                var k = Mathf.SmoothStep(0, 1, t / Shot1);
                position = Vector3.Lerp(_p0, _p1, k);
                rotation = Quaternion.LookRotation(_towerCentre - position);
            }
            else if (t < Shot2)
            {
                var k = Mathf.SmoothStep(0, 1, (t - Shot1) / (Shot2 - Shot1));
                var look = sentryPoint("SENTRY_Slab", 0);
                position = Vector3.Lerp(_p1, framing(look, 9f, 3f), k);
                rotation = Quaternion.Slerp(Quaternion.LookRotation(_towerCentre - _p1), Quaternion.LookRotation(look - position), Mathf.SmoothStep(0, 1, k * 1.6f));
            }
            else if (t < Shot3)
            {
                var k = Mathf.SmoothStep(0, 1, (t - Shot2) / (Shot3 - Shot2));
                var from = framing(sentryPoint("SENTRY_Slab", 0), 9f, 3f);
                var pad = Helipad != null ? Helipad.position : _towerCentre;
                position = Vector3.Lerp(from, framing(pad, 16f, 8f), k);
                rotation = Quaternion.Slerp(Quaternion.LookRotation(sentryPoint("SENTRY_Slab", 0) - from), Quaternion.LookRotation(pad - position), k);
            }
            else
            {
                var k = Mathf.SmoothStep(0, 1, (t - Shot3) / (Length - Shot3));
                var pad = Helipad != null ? Helipad.position : _towerCentre;
                var from = framing(pad, 16f, 8f);
                position = Vector3.Lerp(from, _endPosition, k);
                rotation = Quaternion.Slerp(Quaternion.LookRotation(pad - from), _endRotation, k);
            }
            _camera.transform.SetPositionAndRotation(position, rotation);
            updateOverlay(t);
        }

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
            if (_camera != null) _camera.transform.SetPositionAndRotation(_endPosition, _endRotation);
            VantageCinematic.End();
            if (_music != null) StartCoroutine(fadeOut(_music, 1.5f));
            var whoosh = Whoosh != null ? Whoosh : swoosh();
            if (whoosh != null && _camera != null) AudioSource.PlayClipAtPoint(whoosh, _camera.transform.position, 0.6f);
            if (_canvas != null) Destroy(_canvas.gameObject);
            Finished?.Invoke(skipped, _endedAt);
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
            _caption = VantageUI.Text(_canvas, "Caption", 40, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0, 64), new Vector2(1400, 60));
            _caption.color = Color.clear;
        }

        private void updateOverlay(float t)
        {
            if (_canvas == null) return;
            // Bars slide in over the first half second and out over the last.
            var bars = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((Length - t) / 0.6f);
            var h = 130f * bars;
            _top.sizeDelta = new Vector2(4000, h);
            _bottom.sizeDelta = new Vector2(4000, h);
            _cover.color = new Color(0, 0, 0, 1f - Mathf.Clamp01(t / 0.8f));

            var shot = t < Shot1 ? 0 : t < Shot2 ? 1 : t < Shot3 ? 2 : -1;
            if (shot != _shownCaption)
            {
                _shownCaption = shot;
                _caption.text = shot < 0 ? "" : VantageUI.Caps(Captions[shot]);
                _caption.fontSize = shot == 0 ? 72 : 40;
            }
            var start = shot == 0 ? 0f : shot == 1 ? Shot1 : Shot2;
            var end = shot == 0 ? Shot1 : shot == 1 ? Shot2 : Shot3;
            var a = shot < 0 ? 0f : Mathf.Clamp01((t - start - 0.3f) / 0.5f) * Mathf.Clamp01((end - t) / 0.4f);
            _caption.color = shot == 0 ? new Color(1, 1, 1, a) : new Color(VantageUI.Accent.r, VantageUI.Accent.g, VantageUI.Accent.b, a);
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
