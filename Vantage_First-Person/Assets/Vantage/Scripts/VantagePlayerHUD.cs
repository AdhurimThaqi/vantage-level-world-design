using CoverShooter;
using UnityEngine;
using UnityEngine.UI;

namespace Vantage
{
    /// <summary>
    /// VANTAGE's own HUD for the third-person player, replacing the template's health bar and ammo text.
    /// Built in code at start-up (uGUI), so the scene needs no extra objects:
    /// vitals bottom-left, weapon and ammo bottom-right, hostiles and roof waves top-right,
    /// pickup notices, a damage flash, a low-health pulse and the death screen.
    /// Added automatically by VantageThirdPersonPlayer.
    /// </summary>
    public class VantagePlayerHUD : MonoBehaviour
    {
        public Color Accent = new Color(0.96f, 0.66f, 0.25f);
        public Color Danger = new Color(0.92f, 0.2f, 0.13f);
        public Color PanelColor = new Color(0.04f, 0.05f, 0.06f, 0.72f);
        public Color Dim = new Color(1f, 1f, 1f, 0.12f);
        [Tooltip("Below this fraction of max health the vitals turn red and the screen edges pulse.")]
        [Range(0, 1)] public float CriticalHealth = 0.3f;
        [Tooltip("Hide the template's own health bar and ammo text for this character.")]
        public bool HideTemplateHUD = true;

        private const int Segments = 10;
        private const int MaxPips = 30;

        private CharacterMotor _motor;
        private CharacterHealth _health;
        private VantageThirdPersonPlayer _player;
        private Font _font;
        private GameObject _canvas;

        private RectTransform _vitals, _weapon, _info;
        private Text _hpValue, _hpStatus, _weaponName, _weaponStatus, _ammo, _reserve, _hostiles, _wave, _toast, _death, _deathSub;
        private Image _hpAccent, _flash, _vignette, _deathBackground;
        private readonly Image[] _segments = new Image[Segments];
        private readonly Image[] _pips = new Image[MaxPips];

        private int _kills;
        private float _toastUntil = -1f;
        private float _deathTime = -1f;

        #region Life cycle

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _health = GetComponent<CharacterHealth>();
            _player = GetComponent<VantageThirdPersonPlayer>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            build();
        }

        private void OnEnable()
        {
            VantageEvents.EnemyKilled += onEnemyKilled;
            VantageEvents.WeaponPickedUp += onWeaponPickedUp;
            VantageEvents.LevelCompleted += onLevelCompleted;
        }

        private void OnDisable()
        {
            VantageEvents.EnemyKilled -= onEnemyKilled;
            VantageEvents.WeaponPickedUp -= onWeaponPickedUp;
            VantageEvents.LevelCompleted -= onLevelCompleted;
        }

        private void Start()
        {
            if (HideTemplateHUD)
                hideTemplateHUD();
        }

        private void OnDestroy()
        {
            if (_canvas != null)
                Destroy(_canvas);
        }

        private void hideTemplateHUD()
        {
            foreach (var bar in FindObjectsByType<HealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (bar.Target == gameObject)
                    bar.gameObject.SetActive(false);

            foreach (var ammo in FindObjectsByType<GunAmmo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ammo.Motor == _motor)
                    ammo.gameObject.SetActive(false);
        }

        #endregion

        #region Events

        private void onEnemyKilled(string enemy, Vector3 position) => _kills++;

        private void onWeaponPickedUp(string weapon) => toast(weapon.ToUpperInvariant() + " ACQUIRED");

        private void onLevelCompleted()
        {
            _wave.text = "THE TOWER IS SILENT";
            _wave.color = Color.white;
        }

        private void toast(string text)
        {
            _toast.text = text;
            _toastUntil = Time.time + 2.5f;
        }

        #endregion

        #region Update

        private void LateUpdate()
        {
            var dead = (_player != null && _player.IsDead) || (_motor != null && !_motor.IsAlive);
            _vitals.gameObject.SetActive(!dead);
            _weapon.gameObject.SetActive(!dead);
            _info.gameObject.SetActive(!dead);

            updateVitals();
            updateWeapon();
            updateInfo();
            updateOverlays(dead);
        }

        private void updateVitals()
        {
            if (_health == null)
                return;

            var max = Mathf.Max(1f, _health.MaxHealth);
            var fraction = Mathf.Clamp01(_health.Health / max);
            var critical = fraction <= CriticalHealth;
            var colour = critical ? Danger : Color.white;

            var hp = Mathf.CeilToInt(_health.Health);
            if (hp != _shownHp) { _shownHp = hp; _hpValue.text = hp.ToString(); }
            _hpValue.color = colour;
            _hpAccent.color = critical ? Danger : Accent;

            var filled = fraction * Segments;
            for (int i = 0; i < Segments; i++)
            {
                var fill = Mathf.Clamp01(filled - i);
                _segments[i].color = fill <= 0f ? Dim : Color.Lerp(Dim, critical ? Danger : Accent, 0.35f + 0.65f * fill);
            }

            var sinceDamage = _player != null ? Time.time - _player.LastDamageTime : 999f;
            var regenerating = _player != null && sinceDamage > _player.RegenDelay && fraction < _player.RegenCap;

            if (critical)
            {
                _hpStatus.text = "CRITICAL  ·  FIND COVER";
                _hpStatus.color = Color.Lerp(Danger, Color.white, Mathf.PingPong(Time.time * 2f, 1f) * 0.4f);
            }
            else if (regenerating)
            {
                _hpStatus.text = "STABILISING";
                _hpStatus.color = Accent;
            }
            else
            {
                _hpStatus.text = "VITALS";
                _hpStatus.color = new Color(1f, 1f, 1f, 0.55f);
            }
        }

        private void updateWeapon()
        {
            if (_motor == null)
                return;

            var gun = _motor.IsEquipped ? _motor.Weapon.Gun : null;

            if (gun == null)
            {
                _shownGun = null;
                _shownLoaded = _shownReserve = int.MinValue;
                _weaponName.text = "UNARMED";
                _weaponStatus.text = "FIND A WEAPON";
                _weaponStatus.color = Accent;
                _ammo.text = "--";
                _ammo.color = new Color(1f, 1f, 1f, 0.35f);
                _reserve.text = "";
                for (int i = 0; i < MaxPips; i++)
                    _pips[i].enabled = false;
                return;
            }

            var magazine = gun as Gun;
            var loaded = gun.LoadedBulletsLeft;
            var size = magazine != null ? Mathf.Max(1, magazine.MagazineSize) : Mathf.Max(1, loaded);
            var low = loaded <= Mathf.CeilToInt(size * 0.25f);

            // Strings are only rebuilt when the value changes (no garbage every frame).
            if (gun != _shownGun) { _shownGun = gun; _weaponName.text = gun.Name.ToUpperInvariant(); _shownLoaded = _shownReserve = int.MinValue; }
            if (loaded != _shownLoaded) { _shownLoaded = loaded; _ammo.text = loaded.ToString(); }
            _ammo.color = loaded == 0 ? Danger : low ? Accent : Color.white;
            var reserve = magazine == null ? -1 : magazine.BulletInventory;
            if (reserve != _shownReserve) { _shownReserve = reserve; _reserve.text = reserve < 0 ? "" : reserve >= 999 ? "/ ∞" : "/ " + reserve; }

            if (_motor.IsReloading)
            {
                _weaponStatus.text = "RELOADING";
                _weaponStatus.color = Color.Lerp(Accent, Color.white, Mathf.PingPong(Time.time * 3f, 1f));
            }
            else if (loaded == 0)
            {
                _weaponStatus.text = "EMPTY  ·  PRESS R";
                _weaponStatus.color = Danger;
            }
            else
            {
                _weaponStatus.text = low ? "LOW" : "";
                _weaponStatus.color = Accent;
            }

            var pips = Mathf.Min(size, MaxPips);
            for (int i = 0; i < MaxPips; i++)
            {
                _pips[i].enabled = i < pips;
                if (i < pips)
                    _pips[i].color = i < Mathf.CeilToInt(loaded * pips / (float)size) ? (low ? Accent : Color.white) : Dim;
            }
        }

        private void updateInfo()
        {
            var levels = VantageTowerLevels.Instance;
            if (levels == null || levels.Current < 0)
            {
                showKills();
                return;
            }

            if (_levels != levels)
            {
                _levels = levels;
                levels.LevelCleared += onTowerLevelCleared;
            }
            if (levels.Completed)
            {
                showKills();
                return;
            }
            var remaining = levels.Remaining;
            if (remaining != _shownRemaining) { _shownRemaining = remaining; _shownKills = -1; _hostiles.text = "HOSTILES LEFT   " + remaining; }
            if (levels.Current != _shownLevel && _wave.color != Color.white)
            {
                _shownLevel = levels.Current;
                _wave.text = $"LEVEL {levels.Current + 1}/{levels.Levels.Count}  ·  {levels.CurrentName.ToUpperInvariant()}";
                _wave.color = Accent;
            }
        }

        private void showKills()
        {
            if (_kills == _shownKills) return;
            _shownKills = _kills;
            _shownRemaining = -1;
            _hostiles.text = "HOSTILES DOWN   " + _kills;
        }

        // Last values written to the texts, so strings are only built when something changed.
        private int _shownHp = int.MinValue, _shownLoaded = int.MinValue, _shownReserve = int.MinValue;
        private int _shownRemaining = -1, _shownKills = -1, _shownLevel = -1;
        private BaseGun _shownGun;

        private VantageTowerLevels _levels;

        private void onTowerLevelCleared(int level)
        {
            var l = _levels.Levels[level];
            toast(string.IsNullOrEmpty(l.ClearedMessage) ? $"LEVEL {level + 1} CLEARED  ·  STAIRS UNLOCKED" : l.ClearedMessage);
        }

        private void updateOverlays(bool dead)
        {
            // Damage flash.
            var flashTime = _player != null ? _player.DamageFlashTime : 0.35f;
            var sinceDamage = _player != null ? Time.time - _player.LastDamageTime : 999f;
            var flash = Mathf.Clamp01(1f - sinceDamage / Mathf.Max(0.01f, flashTime));
            _flash.color = new Color(Danger.r, 0f, 0f, 0.28f * flash);

            // Low health: the screen edges pulse red.
            var fraction = _health != null ? _health.Health / Mathf.Max(1f, _health.MaxHealth) : 1f;
            var pulse = !dead && fraction <= CriticalHealth ? 0.45f + 0.35f * Mathf.Sin(Time.time * 5f) : 0f;
            _vignette.color = new Color(Danger.r, 0.02f, 0.02f, Mathf.Max(pulse, 0.6f * flash));

            // Pickup notice.
            var toastAlpha = Mathf.Clamp01((_toastUntil - Time.time) / 0.5f);
            _toast.color = new Color(Accent.r, Accent.g, Accent.b, toastAlpha);

            // Death screen.
            if (dead && _deathTime < 0)
                _deathTime = Time.time;
            var deathAlpha = _deathTime < 0 ? 0f : Mathf.Clamp01((Time.time - _deathTime) / 0.8f);
            _deathBackground.color = new Color(0, 0, 0, 0.7f * deathAlpha);
            _death.color = new Color(Danger.r, Danger.g, Danger.b, deathAlpha);
            _deathSub.color = new Color(1, 1, 1, 0.7f * deathAlpha);
        }

        #endregion

        #region Building

        private void build()
        {
            _canvas = new GameObject("VANTAGE HUD");
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)_canvas.transform;

            // Full-screen layers first, so the panels draw on top.
            _vignette = image(stretch("Low Health Vignette", root), Color.clear);
            _vignette.sprite = vignetteSprite();
            _flash = image(stretch("Damage Flash", root), Color.clear);

            // Vitals, bottom-left.
            _vitals = panel("Vitals", root, new Vector2(0, 0), new Vector2(40, 40), new Vector2(430, 104));
            _hpAccent = image(rect("Accent", _vitals, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(6, 104)), Accent);
            _hpValue = text(_vitals, "HP", 54, TextAnchor.LowerLeft, FontStyle.Bold, new Vector2(22, 10), new Vector2(130, 70));
            _hpStatus = text(_vitals, "Status", 15, TextAnchor.UpperLeft, FontStyle.Bold, new Vector2(150, 62), new Vector2(270, 26));
            for (int i = 0; i < Segments; i++)
                _segments[i] = image(rect("Segment", _vitals, Vector2.zero, Vector2.zero, new Vector2(150 + i * 26, 22), new Vector2(23, 30)), Dim);

            // Weapon, bottom-right.
            _weapon = panel("Weapon", root, new Vector2(1, 0), new Vector2(-40, 40), new Vector2(430, 104));
            image(rect("Accent", _weapon, new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(6, 104)), Accent);
            _weaponName = text(_weapon, "Name", 18, TextAnchor.UpperLeft, FontStyle.Bold, new Vector2(20, 66), new Vector2(200, 28));
            _weaponName.color = Accent;
            _weaponStatus = text(_weapon, "Status", 15, TextAnchor.UpperLeft, FontStyle.Bold, new Vector2(20, 44), new Vector2(220, 24));
            _ammo = text(_weapon, "Ammo", 58, TextAnchor.LowerRight, FontStyle.Bold, new Vector2(230, 8), new Vector2(110, 80));
            _reserve = text(_weapon, "Reserve", 22, TextAnchor.LowerLeft, FontStyle.Normal, new Vector2(346, 18), new Vector2(76, 32));
            _reserve.color = new Color(1, 1, 1, 0.6f);
            for (int i = 0; i < MaxPips; i++)
                _pips[i] = image(rect("Round", _weapon, Vector2.zero, Vector2.zero, new Vector2(20 + i * 7, 18), new Vector2(4, 16)), Dim);

            // Hostiles and waves, top-right (top-left belongs to the playtest overlay).
            _info = panel("Info", root, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(330, 70));
            image(rect("Accent", _info, new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(6, 70)), Accent);
            _hostiles = text(_info, "Hostiles", 18, TextAnchor.UpperRight, FontStyle.Bold, new Vector2(10, 36), new Vector2(300, 26));
            _wave = text(_info, "Wave", 15, TextAnchor.UpperRight, FontStyle.Bold, new Vector2(10, 10), new Vector2(300, 24));
            _wave.text = "";
            _wave.color = new Color(1, 1, 1, 0.6f);

            // Pickup notice, below the centre.
            _toast = text(root, "Pickup Notice", 30, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(900, 50));
            setAnchor(_toast.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero);
            _toast.color = Color.clear;

            // Death screen.
            _deathBackground = image(stretch("Death Background", root), Color.clear);
            _death = text(root, "Death", 72, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(1200, 100));
            setAnchor(_death.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero);
            _death.text = "SIGNAL LOST";
            _death.color = Color.clear;
            _deathSub = text(root, "Death Sub", 20, TextAnchor.MiddleCenter, FontStyle.Normal, Vector2.zero, new Vector2(1200, 40));
            setAnchor(_deathSub.rectTransform, new Vector2(0.5f, 0.47f), new Vector2(0.5f, 0.5f), Vector2.zero);
            _deathSub.text = "restarting the climb...";
            _deathSub.color = Color.clear;
        }

        private RectTransform panel(string name, RectTransform parent, Vector2 corner, Vector2 offset, Vector2 size)
        {
            var r = rect(name, parent, corner, corner, offset, size);
            image(r, PanelColor);
            return r;
        }

        private static RectTransform rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            setAnchor(r, anchor, pivot, position);
            r.sizeDelta = size;
            return r;
        }

        private static void setAnchor(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = position;
        }

        private static RectTransform stretch(string name, Transform parent)
        {
            var r = rect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return r;
        }

        private static Image image(RectTransform r, Color color)
        {
            var i = r.gameObject.AddComponent<Image>();
            i.color = color;
            i.raycastTarget = false;
            return i;
        }

        /// <summary>
        /// A text whose position is measured from its parent's bottom-left corner.
        /// </summary>
        private Text text(RectTransform parent, string name, int size, TextAnchor alignment, FontStyle style, Vector2 position, Vector2 box)
        {
            var r = rect(name, parent, Vector2.zero, Vector2.zero, position, box);
            var t = r.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = alignment;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = r.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        /// <summary>
        /// Transparent in the middle, opaque at the edges; tinted red by the image colour.
        /// </summary>
        private static Sprite vignetteSprite()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size * 2f - 1f;
                    var v = (y + 0.5f) / size * 2f - 1f;
                    var d = Mathf.Sqrt(u * u + v * v) / Mathf.Sqrt(2f);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d)));
                }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        #endregion
    }
}
