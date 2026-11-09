using System.Collections;
using System.Collections.Generic;
using CoverShooter;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vantage
{
    /// <summary>
    /// The title screen, once per session before the intro, in the level itself: from low on the ground the camera
    /// sways slowly in front of the tower's sunlit side (the entrance side, turned a little so the broken top floor
    /// and the slab show), with the golden horizon behind the menu, while the extraction helicopter circles the base
    /// with a searchlight. Left: the title and Play / Settings /
    /// Credits / Quit (mouse, or W/S and Enter; Esc goes back). Play hands the camera, where it is, to VantageIntro.
    /// Settings (master, music and effects volume, mouse sensitivity, graphics quality, fullscreen) are kept in
    /// PlayerPrefs and applied on every scene load (the volumes by VantageAudio, through the mixer).
    /// </summary>
    public class VantageMainMenu : MonoBehaviour
    {
        [Tooltip("Shown along the bottom.")]
        public string Tagline = "Get in. Fight up. Get out.";
        [TextArea(6, 20)]
        public string Credits =
            "Design, level, code and 3D models\nAdhurim Thaqi\n\n" +
            "HSLU  Level and World Design  HS26\n\n" +
            "Course template: Third Person Cover Shooter, Pistol Animset Pro\n" +
            "Asset Store: Low Poly Soldiers (demo), ISO Mech, Stylized Coin Pack\n" +
            "Textures: Poly Haven (CC0)\n" +
            "Player character: Avaturn";

        public static VantageMainMenu Instance { get; private set; }
        /// <summary>The menu is up (the intro waits for Play).</summary>
        public bool Showing { get; private set; }
        /// <summary>Showing, or about to show on this load (the level music waits for it: VantageMusic).</summary>
        public bool Holding => Showing || _pending;
        [Tooltip("Fade-out of the title-screen music (MUS_Menu) after Play, in seconds.")]
        public float MusicFade = 1.5f;

        private const string SensitivityKey = "vantage.sensitivity", QualityKey = "vantage.quality";
        private const float SwayDegrees = 8f, SwaySpeed = 0.11f, Distance = 38f, Height = 1.8f, Turn = 20f;

        private enum Page { Main, Settings, Credits }
        private Page _page;
        private int _selected;
        private bool _play;
        private float _shownAt;

        private Camera _camera;
        private Vector3 _centre, _toCamera, _lookAt;
        private RectTransform _canvas;
        private Image _cover;
        private readonly List<Row> _main = new List<Row>(), _settings = new List<Row>();
        private RectTransform _mainPanel, _settingsPanel, _creditsPanel;
        private readonly List<Behaviour> _mouseLocks = new List<Behaviour>();
        private GameObject _helicopter;
        private AudioSource _rotor;
        private AudioSource _music;
        private bool _pending;
        private Transform _mainRotor, _tailRotor;
        private float _heliAngle;

        private class Row
        {
            public RectTransform Rect;
            public Image Frame, Fill, Bar;
            public TMP_Text Label, Value;
            public string Name;
            public System.Action<int> Act;   // 0 choose, -1 / +1 change a setting
        }

        private void Awake()
        {
            Instance = this;
            ApplySettings();
            if (!VantageRun.MenuShown) build();   // black from the first frame, until the intro hands over the camera
            // The intro runs the menu on a session's first run (VantageIntro.Start: checkpoint 0, intro not played).
            _pending = !VantageRun.MenuShown && (!VantageRun.Started || (VantageRun.Checkpoint == 0 && !VantageRun.IntroPlayed));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (_helicopter != null) Destroy(_helicopter);
        }

        /// <summary>Volumes, mouse sensitivity and quality from PlayerPrefs (every scene load; the menu or not).</summary>
        public static void ApplySettings()
        {
            VantageAudio.ApplyVolumes();
            if (PlayerPrefs.HasKey(QualityKey))
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(QualityKey), 0, QualitySettings.names.Length - 1), true);
            var sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            foreach (var input in FindObjectsByType<ThirdPersonInput>(FindObjectsSortMode.None))
            {
                input.HorizontalRotateSpeed *= sensitivity;   // the prefab's speeds are fresh on every load
                input.VerticalRotateSpeed *= sensitivity;
            }
        }

        /// <summary>Shows the menu with the camera VantageIntro has taken over; ends when Play is chosen.</summary>
        public IEnumerator Run(Camera camera, Vector3 towerCentre, Bounds towerArea, Vector3 entranceFacing)
        {
            _camera = camera;
            _centre = new Vector3(towerCentre.x, towerArea.min.y, towerCentre.z);
            _lookAt = _centre + Vector3.up * towerArea.size.y * 0.8f;   // looking up at it from the ground
            entranceFacing.y = 0;
            _toCamera = Quaternion.Euler(0, Turn, 0) * (entranceFacing.sqrMagnitude > 0.01f ? entranceFacing.normalized : Vector3.back);
            foreach (var m in FindObjectsByType<MouseLock>(FindObjectsSortMode.None))
                if (m.enabled) { m.enabled = false; _mouseLocks.Add(m); }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_canvas == null) build();
            spawnHelicopter();
            Showing = true;
            _pending = false;
            _shownAt = Time.time;
            _music = VantageAudio.PlayLoop(VantageSounds.MusMenu, null);
            while (!_play) yield return null;

            Showing = false;
            VantageAudio.Stop(_music, MusicFade);
            Destroy(_canvas.gameObject);
            if (_helicopter != null) Destroy(_helicopter);
            VantageAudio.Stop(_rotor, 1f);
            foreach (var m in _mouseLocks) if (m != null) m.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Starts the game (also used by the playtest bot).</summary>
        public void Play()
        {
            VantageAudio.Play2D(VantageSounds.UiSelect);
            _play = true;
        }

        public void OpenSettings() => show(Page.Settings);
        public void OpenCredits() => show(Page.Credits);
        public void Back() => show(Page.Main);

        private void Update()
        {
            if (!Showing) return;
            var t = Time.time - _shownAt;
            if (_cover != null) _cover.color = new Color(0, 0, 0, 1f - Mathf.Clamp01(t / 1.5f));
            flyHelicopter();

            var rows = _page == Page.Main ? _main : _page == Page.Settings ? _settings : null;
            if (Input.GetKeyDown(KeyCode.Escape) && _page != Page.Main) { Back(); return; }
            if (rows == null)
            {
                if (Input.anyKeyDown) Back();
                return;
            }
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(-1);
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(+1);
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) rows[_selected].Act(-1);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) rows[_selected].Act(+1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) rows[_selected].Act(0);

            // Mouse: hover selects, a click chooses (on a setting: left half lowers, right half raises).
            if (Input.mousePosition == _lastMouse && !Input.GetMouseButtonDown(0)) return;
            _lastMouse = Input.mousePosition;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(rows[i].Rect, Input.mousePosition, null)) continue;
                if (i != _selected) select(i);
                if (Input.GetMouseButtonDown(0))
                {
                    if (rows[i].Value == null) rows[i].Act(0);
                    else
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(rows[i].Rect, Input.mousePosition, null, out var local);
                        rows[i].Act(local.x < rows[i].Rect.rect.center.x ? -1 : +1);
                    }
                }
            }
        }

        private Vector3 _lastMouse;

        private void LateUpdate()
        {
            if (!Showing || _camera == null) return;
            var t = Time.time - _shownAt;
            var dir = Quaternion.Euler(0, Mathf.Sin(t * SwaySpeed) * SwayDegrees, 0) * _toCamera;
            var push = Mathf.Lerp(1.1f, 1f, Mathf.SmoothStep(0, 1, t / 8f));   // a slow push-in as it opens
            var position = _centre + dir * Distance * push + Vector3.up * (Height + Mathf.Sin(t * 0.23f) * 0.4f);
            // Turned a little to the left, so the tower stands right of centre, clear of the menu.
            var look = Quaternion.LookRotation(_lookAt - position) * Quaternion.Euler(0, -15f, 0);
            _camera.transform.SetPositionAndRotation(position, look);
        }

        #region Helicopter

        private void spawnHelicopter()
        {
            var extraction = FindAnyObjectByType<VantageExtraction>();
            if (extraction == null || extraction.HelicopterPrefab == null) return;
            _helicopter = Instantiate(extraction.HelicopterPrefab);
            _helicopter.name = "Menu Helicopter";
            _rotor = VantageAudio.PlayLoop(VantageSounds.HeliRotor, _helicopter.transform);
            foreach (var c in _helicopter.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var t in _helicopter.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Main Rotor") _mainRotor = t;
                if (t.name == "Tail Rotor") _tailRotor = t;
            }
            var spot = new GameObject("Searchlight").AddComponent<Light>();
            spot.transform.SetParent(_helicopter.transform, false);
            spot.transform.localPosition = new Vector3(0, -0.6f, 2f);
            spot.transform.localRotation = Quaternion.Euler(62f, 0, 0);
            spot.type = LightType.Spot;
            spot.color = new Color(0.9f, 0.94f, 1f);
            spot.range = 110f;
            spot.spotAngle = 16f;
            spot.innerSpotAngle = 8f;
            spot.intensity = 1800f;
            spot.shadows = LightShadows.None;
            _heliAngle = Mathf.Atan2(_toCamera.x, _toCamera.z) + Mathf.PI - 0.7f;   // starts beyond the tower, crossing the view
        }

        private void flyHelicopter()
        {
            if (_helicopter == null) return;
            const float radius = 80f, speed = 0.09f;
            _heliAngle += speed * Time.deltaTime;
            var offset = new Vector3(Mathf.Sin(_heliAngle), 0, Mathf.Cos(_heliAngle)) * radius;
            var tangent = new Vector3(Mathf.Cos(_heliAngle), 0, -Mathf.Sin(_heliAngle));
            _helicopter.transform.position = _centre + offset + Vector3.up * (34f + Mathf.Sin(Time.time * 0.4f) * 1.5f);
            _helicopter.transform.rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(4f, 0, -9f);
            if (_mainRotor != null) _mainRotor.Rotate(0, 900f * Time.deltaTime, 0, Space.Self);
            if (_tailRotor != null) _tailRotor.Rotate(1400f * Time.deltaTime, 0, 0, Space.Self);
        }

        #endregion

        #region Pages

        private void build()
        {
            _canvas = VantageUI.Canvas("VANTAGE Main Menu", 80);
            // A dark falloff behind the menu side, like a vignette from the left.
            var shade = VantageUI.Rect("Shade", _canvas, new Vector2(0, 0.5f), Vector2.zero, new Vector2(1300, 1400));
            VantageUI.Image(shade, new Color(0, 0, 0, 0.72f), VantageUI.Shade).rectTransform.anchoredPosition = new Vector2(-520, 0);
            VantageUI.Image(VantageUI.Stretch("Vignette", _canvas), new Color(0, 0, 0, 0.55f), VantageUI.Vignette);

            var title = VantageUI.Text(_canvas, "Title", 168, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(110, -150), new Vector2(900, 170));
            title.text = "VANTAGE";
            title.characterSpacing = 6;
            VantageUI.Image(VantageUI.Rect("Line", _canvas, new Vector2(0, 1), new Vector2(110, -330), new Vector2(560, 3)), VantageUI.Danger, VantageUI.Pixel);
            var subtitle = VantageUI.Text(_canvas, "Subtitle", 34, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(112, -350), new Vector2(900, 40));
            subtitle.text = "THE TOWER IS SILENT";
            subtitle.characterSpacing = 18;
            subtitle.color = new Color(0.85f, 0.85f, 0.85f);
            var tag = VantageUI.Text(_canvas, "Tagline", 24, TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(110, 60), new Vector2(900, 30));
            tag.text = VantageUI.Caps(Tagline);
            tag.characterSpacing = 14;
            tag.color = VantageUI.Muted;

            _mainPanel = VantageUI.Rect("Main", _canvas, new Vector2(0, 0.5f), new Vector2(100, -90), new Vector2(560, 330));
            _main.Add(row(_mainPanel, _main.Count, "Play game", null, _ => Play()));
            _main.Add(row(_mainPanel, _main.Count, "Settings", null, _ => OpenSettings()));
            _main.Add(row(_mainPanel, _main.Count, "Credits", null, _ => OpenCredits()));
            _main.Add(row(_mainPanel, _main.Count, "Quit", null, _ => quit()));

            _settingsPanel = VantageUI.Rect("Settings", _canvas, new Vector2(0, 0.5f), new Vector2(100, -160), new Vector2(760, 530));
            _settings.Add(row(_settingsPanel, 0, "Master", "", d => volume(VantageAudio.MasterKey, d)));
            _settings.Add(row(_settingsPanel, 1, "Music", "", d => volume(VantageAudio.MusicKey, d)));
            _settings.Add(row(_settingsPanel, 2, "Effects", "", d => volume(VantageAudio.EffectsKey, d)));
            _settings.Add(row(_settingsPanel, 3, "Mouse sensitivity", "", d => change(SensitivityKey, d, 0.4f, 2f, 0.1f, 1f)));
            _settings.Add(row(_settingsPanel, 4, "Graphics", "", d => quality(d)));
            _settings.Add(row(_settingsPanel, 5, "Fullscreen", "", _ => { Screen.fullScreen = !Screen.fullScreen; refresh(); }));
            _settings.Add(row(_settingsPanel, 6, "Back", null, _ => Back()));

            _creditsPanel = VantageUI.Rect("Credits", _canvas, new Vector2(0, 0.5f), new Vector2(110, -90), new Vector2(900, 420));
            var credits = VantageUI.Text(_creditsPanel, "Text", 26, TextAlignmentOptions.TopLeft, new Vector2(0, 1), Vector2.zero, new Vector2(900, 420));
            credits.textWrappingMode = TextWrappingModes.Normal;
            credits.text = Credits + "\n\n<color=#ffffff88>ANY KEY: BACK</color>";

            _cover = VantageUI.Image(VantageUI.Stretch("Fade", _canvas), Color.black);
            show(Page.Main);
        }

        private Row row(RectTransform panel, int index, string label, string value, System.Action<int> act)
        {
            var r = new Row { Name = label, Act = act };
            r.Rect = VantageUI.Rect(label, panel, new Vector2(0, 1), new Vector2(0, -index * 78), new Vector2(panel.sizeDelta.x, 60));
            r.Fill = VantageUI.Image(VantageUI.Stretch("Fill", r.Rect), new Color(VantageUI.Danger.r, VantageUI.Danger.g, VantageUI.Danger.b, 0.16f), VantageUI.Pixel);
            r.Frame = VantageUI.Image(VantageUI.Stretch("Frame", r.Rect), VantageUI.Danger, frameSprite());
            r.Frame.type = Image.Type.Sliced;
            r.Bar = VantageUI.Image(VantageUI.Rect("Bar", r.Rect, new Vector2(0, 0.5f), Vector2.zero, new Vector2(6, 60)), VantageUI.Danger, VantageUI.Pixel);
            r.Label = VantageUI.Text(r.Rect, "Label", 34, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(420, 50));
            r.Label.text = VantageUI.Caps(label);
            if (value != null)
            {
                r.Value = VantageUI.Text(r.Rect, "Value", 30, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(300, 50));
                r.Value.color = VantageUI.Accent;
            }
            return r;
        }

        private static Sprite _frame;

        /// <summary>A 2-pixel outline, sliced so it stays thin at any size.</summary>
        private static Sprite frameSprite()
        {
            if (_frame != null) return _frame;
            const int n = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tex.SetPixel(x, y, x < 2 || y < 2 || x >= n - 2 || y >= n - 2 ? Color.white : Color.clear);
            tex.Apply();
            return _frame = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
        }

        private void show(Page page)
        {
            if (_mainPanel != null && page != _page) VantageAudio.Play2D(VantageSounds.UiSelect);
            _page = page;
            _mainPanel.gameObject.SetActive(page == Page.Main);
            _settingsPanel.gameObject.SetActive(page == Page.Settings);
            _creditsPanel.gameObject.SetActive(page == Page.Credits);
            select(0);
            refresh();
        }

        /// <summary>The settings page's row names, top to bottom.</summary>
        public IEnumerable<string> SettingNames
        {
            get { foreach (var r in _settings) yield return r.Name; }
        }

        /// <summary>Acts on a settings row: -1 / +1 change it, 0 chooses it (A/D, Enter, the mouse; also the bot).</summary>
        public void ChangeSetting(int index, int step)
        {
            if (index >= 0 && index < _settings.Count) _settings[index].Act(step);
        }

        /// <summary>A volume row: 0–1 in tenths, applied at once (the old single volume seeds Master).</summary>
        private void volume(string key, int step)
        {
            if (!PlayerPrefs.HasKey(key)) PlayerPrefs.SetFloat(key, VantageAudio.Setting(key));
            change(key, step, 0f, 1f, 0.1f, 1f);
            VantageAudio.ApplyVolumes();
        }

        /// <summary>Moves the highlight up (-1) or down (+1) the current page, wrapping (W/S; also the bot).</summary>
        public void MoveSelection(int step)
        {
            var rows = _page == Page.Main ? _main : _page == Page.Settings ? _settings : null;
            var count = rows != null ? rows.Count : 0;
            if (count > 0) select(((_selected + step) % count + count) % count);
        }

        private void select(int index)
        {
            if (index != _selected && Showing) VantageAudio.Play2D(VantageSounds.UiMove);
            _selected = index;
            var rows = _page == Page.Main ? _main : _settings;
            for (int i = 0; i < rows.Count; i++)
            {
                var on = i == index;
                rows[i].Frame.enabled = rows[i].Fill.enabled = rows[i].Bar.enabled = on;
                rows[i].Label.color = on ? Color.white : new Color(1, 1, 1, 0.72f);
            }
        }

        private void refresh()
        {
            if (_settings.Count < 6) return;
            _settings[0].Value.text = $"< {Mathf.RoundToInt(VantageAudio.Setting(VantageAudio.MasterKey) * 100)} % >";
            _settings[1].Value.text = $"< {Mathf.RoundToInt(VantageAudio.Setting(VantageAudio.MusicKey) * 100)} % >";
            _settings[2].Value.text = $"< {Mathf.RoundToInt(VantageAudio.Setting(VantageAudio.EffectsKey) * 100)} % >";
            _settings[3].Value.text = $"< {PlayerPrefs.GetFloat(SensitivityKey, 1f):0.0}x >";
            _settings[4].Value.text = $"< {VantageUI.Caps(QualitySettings.names[QualitySettings.GetQualityLevel()])} >";
            _settings[5].Value.text = Screen.fullScreen ? "< ON >" : "< OFF >";
        }

        private void change(string key, int step, float min, float max, float by, float fallback)
        {
            var before = PlayerPrefs.GetFloat(key, fallback);
            var value = Mathf.Clamp(Mathf.Round((before + (step == 0 ? by : step * by)) * 10f) / 10f, min, max);
            if (step == 0 && Mathf.Approximately(before, max)) value = min;   // Enter cycles round
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            if (key == SensitivityKey)
                foreach (var input in FindObjectsByType<ThirdPersonInput>(FindObjectsSortMode.None))
                {
                    input.HorizontalRotateSpeed *= value / before;
                    input.VerticalRotateSpeed *= value / before;
                }
            refresh();
        }

        private void quality(int step)
        {
            var count = QualitySettings.names.Length;
            var level = (QualitySettings.GetQualityLevel() + (step < 0 ? count - 1 : 1)) % count;
            QualitySettings.SetQualityLevel(level, true);
            PlayerPrefs.SetInt(QualityKey, level);
            PlayerPrefs.Save();
            refresh();
        }

        private static void quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion
    }
}
