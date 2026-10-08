using CoverShooter;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vantage
{
    /// <summary>
    /// The player's HUD, built in code at start-up (uGUI) and added by VantageThirdPersonPlayer.
    /// Kept to what the player acts on, without boxes or labels:
    /// - bottom-left: health number and bar (a trailing layer shows the damage just taken, a tick marks how far
    ///   health regenerates);
    /// - bottom-right: weapon, loaded rounds and reserve;
    /// - top-centre: level, its name and one pip per enemy on the level (dark when killed);
    /// - centre: the aimed-at enemy's health, a kill marker, pickup notices and a level-cleared banner;
    /// - full screen: damage flash, low-health vignette and the death screen.
    /// </summary>
    public class VantagePlayerHUD : MonoBehaviour
    {
        [Tooltip("Below this fraction of max health the health turns red and the screen edges pulse.")]
        [Range(0, 1)] public float CriticalHealth = 0.3f;

        private const int MaxPips = 16;
        private const float Margin = 56f;

        private CharacterMotor _motor;
        private CharacterHealth _health;
        private VantageThirdPersonPlayer _player;
        private RectTransform _canvas, _hud;

        private TMP_Text _hp, _ammo, _reserve, _weapon, _levelNumber, _levelName, _hint, _notice, _bannerTitle, _bannerSub, _death, _deathSub;
        private Image _hpFill, _hpTrail, _regenCap, _targetFill, _targetTrail, _flash, _vignette, _deathBackground;
        private RectTransform _kill, _target;
        private readonly Image[] _pips = new Image[MaxPips];

        private float _trail = 1f, _targetTrailValue = 1f;
        private float _noticeUntil = -1f, _bannerAt = -10f, _killAt = -10f, _targetSeen = -10f, _deathTime = -1f;

        // Last values written, so strings are only built when something changed (no garbage every frame).
        private int _shownHp = int.MinValue, _shownLoaded = int.MinValue, _shownReserve = int.MinValue, _shownLevel = -2, _levelTotal;
        private BaseGun _shownGun;
        private GameObject _targetObject;
        private CharacterHealth _targetHealth;
        private VantageTowerLevels _levels;

        #region Life cycle

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _health = GetComponent<CharacterHealth>();
            _player = GetComponent<VantageThirdPersonPlayer>();
            build();
        }

        private void OnEnable()
        {
            VantageEvents.EnemyKilled += onEnemyKilled;
            VantageEvents.WeaponPickedUp += onWeaponPickedUp;
        }

        private void OnDisable()
        {
            VantageEvents.EnemyKilled -= onEnemyKilled;
            VantageEvents.WeaponPickedUp -= onWeaponPickedUp;
            if (_levels != null) _levels.LevelCleared -= onLevelCleared;
        }

        private void OnDestroy()
        {
            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }

        #endregion

        #region Events

        private void onEnemyKilled(string enemy, Vector3 position) => _killAt = Time.time;

        private void onWeaponPickedUp(string weapon)
        {
            _notice.text = VantageUI.Caps("+ " + weapon);
            _noticeUntil = Time.time + 2.2f;
        }

        /// <summary>"LEVEL 1 CLEAR · RIFLE UNLOCKED · STAIRS OPEN" → title "LEVEL 1 CLEAR", subtitle the rest.</summary>
        private void onLevelCleared(int level)
        {
            var message = _levels.Levels[level].ClearedMessage;
            if (string.IsNullOrEmpty(message)) message = $"LEVEL {level + 1} CLEAR · STAIRS OPEN";
            var parts = message.Split('·');
            _bannerTitle.text = VantageUI.Caps(parts[0].Trim());
            _bannerSub.text = parts.Length > 1 ? VantageUI.Caps(string.Join("  ·  ", System.Array.ConvertAll(parts, p => p.Trim()), 1, parts.Length - 1)) : "";
            _bannerAt = Time.time;
        }

        #endregion

        #region Update

        private void LateUpdate()
        {
            var dead = (_player != null && _player.IsDead) || (_motor != null && !_motor.IsAlive);
            _hud.gameObject.SetActive(!dead);
            if (!dead)
            {
                updateHealth();
                updateWeapon();
                updateLevel();
                updateTarget();
                updateCentre();
            }
            updateOverlays(dead);
        }

        private void updateHealth()
        {
            if (_health == null)
                return;

            var fraction = Mathf.Clamp01(_health.Health / Mathf.Max(1f, _health.MaxHealth));
            var critical = fraction <= CriticalHealth;
            var hp = Mathf.CeilToInt(_health.Health);
            if (hp != _shownHp) { _shownHp = hp; _hp.text = hp.ToString(); }
            _hp.color = critical ? Danger : Color.white;

            _hpFill.fillAmount = fraction;
            _hpFill.color = critical ? Danger : Color.white;
            // The trail waits a moment after a hit, then catches up: shows how much that hit cost.
            _trail = fraction >= _trail ? fraction : Time.time - lastDamage > 0.6f ? Mathf.MoveTowards(_trail, fraction, Time.deltaTime * 0.8f) : _trail;
            _hpTrail.fillAmount = _trail;

            if (_player != null)
            {
                _regenCap.rectTransform.anchorMin = _regenCap.rectTransform.anchorMax = new Vector2(_player.RegenCap, 0.5f);
                var regenerating = Time.time - lastDamage > _player.RegenDelay && fraction < _player.RegenCap;
                _regenCap.color = regenerating ? Color.Lerp(Muted, Color.white, Mathf.PingPong(Time.time * 2f, 1f)) : Muted;
            }
        }

        private float lastDamage => _player != null ? _player.LastDamageTime : -100f;

        private void updateWeapon()
        {
            if (_motor == null)
                return;

            var gun = _motor.IsEquipped ? _motor.Weapon.Gun : null;
            if (gun == null)
            {
                if (_shownGun != null || _shownLoaded != -1)
                {
                    _shownGun = null;
                    _shownLoaded = -1;
                    _shownReserve = int.MinValue;
                    _ammo.text = "";
                    _reserve.text = "";
                    _weapon.text = "NO WEAPON";
                }
                _weapon.color = Muted;
                return;
            }

            var magazine = gun as Gun;
            var loaded = gun.LoadedBulletsLeft;
            var size = magazine != null ? Mathf.Max(1, magazine.MagazineSize) : Mathf.Max(1, loaded);
            var low = loaded <= Mathf.CeilToInt(size * 0.25f);

            if (loaded != _shownLoaded) { _shownLoaded = loaded; _ammo.text = loaded.ToString(); }
            var reserve = magazine == null ? -1 : magazine.BulletInventory;
            if (reserve != _shownReserve) { _shownReserve = reserve; _reserve.text = reserve < 0 ? "" : reserve >= 999 ? "/ ∞" : "/ " + reserve; }
            _ammo.color = loaded == 0 ? Danger : low ? Accent : Color.white;

            // The weapon line doubles as the status line.
            string line; Color colour;
            if (_motor.IsReloading) { line = "RELOADING"; colour = Color.Lerp(Accent, Color.white, Mathf.PingPong(Time.time * 3f, 1f)); }
            else if (loaded == 0) { line = "EMPTY  ·  R TO RELOAD"; colour = Danger; }
            else { line = null; colour = Muted; }
            if (line != null) { _weapon.text = line; _shownGun = null; }
            else if (gun != _shownGun) { _shownGun = gun; _weapon.text = VantageUI.Caps(gun.Name); }
            _weapon.color = colour;
        }

        private void updateLevel()
        {
            var levels = VantageTowerLevels.Instance;
            if (levels != _levels)
            {
                if (_levels != null) _levels.LevelCleared -= onLevelCleared;
                _levels = levels;
                if (_levels != null) _levels.LevelCleared += onLevelCleared;
            }

            var unarmed = _motor != null && !_motor.IsEquipped;
            _hint.text = unarmed ? "FIND A WEAPON"
                       : levels != null && levels.Hunting ? "THE LAST HOSTILES ARE COMING FOR YOU"
                       : "";

            if (levels == null || levels.Current < 0)
            {
                setPips(0, 0);
                return;
            }

            if (levels.Current != _shownLevel)
            {
                _shownLevel = levels.Current;
                _levelTotal = 0;
                if (levels.Completed)
                {
                    _levelNumber.text = VantageUI.Caps("All levels clear");
                    _levelName.text = VantageUI.Caps("The tower is silent");
                }
                else
                {
                    _levelNumber.text = VantageUI.Caps($"Level {levels.Current + 1} / {levels.Levels.Count}");
                    _levelName.text = VantageUI.Caps(levels.CurrentName);
                }
            }
            if (levels.Completed)
            {
                setPips(0, 0);
                return;
            }
            var remaining = levels.Remaining;
            _levelTotal = Mathf.Max(_levelTotal, remaining);
            setPips(_levelTotal, remaining);
        }

        /// <summary>One pip per enemy on the level: the living ones red, the dead ones faint.</summary>
        private void setPips(int total, int alive)
        {
            total = Mathf.Min(total, MaxPips);
            const float spacing = 18f;
            for (int i = 0; i < MaxPips; i++)
            {
                var pip = _pips[i];
                pip.enabled = i < total;
                if (!pip.enabled) continue;
                pip.rectTransform.anchoredPosition = new Vector2((i - (total - 1) / 2f) * spacing, -126f);
                pip.color = i < alive ? Danger : Faint;
            }
        }

        /// <summary>A small health bar under the crosshair for the enemy the player is aiming at.</summary>
        private void updateTarget()
        {
            var target = _motor != null ? _motor.AskForTarget() : null;
            if (target != null && target != _targetObject)
            {
                _targetObject = target;
                var actor = target.GetComponent<BaseActor>();
                _targetHealth = actor != null && _player != null && actor.Side != _player.Side ? CharacterHealth.Get(target) : null;
                _targetTrailValue = 1f;
            }

            if (target != null && _targetHealth != null && _targetHealth.Health > 0)
            {
                _targetSeen = Time.time;
                var fraction = Mathf.Clamp01(_targetHealth.Health / Mathf.Max(1f, _targetHealth.MaxHealth));
                _targetFill.fillAmount = fraction;
                _targetTrailValue = Mathf.MoveTowards(Mathf.Max(_targetTrailValue, fraction), fraction, Time.deltaTime * 0.8f);
                _targetTrail.fillAmount = _targetTrailValue;
            }
            var alpha = Mathf.Clamp01(1f - (Time.time - _targetSeen - 0.8f) / 0.3f);
            _target.gameObject.SetActive(alpha > 0f);
            _target.localScale = new Vector3(1f, alpha, 1f);
        }

        private void updateCentre()
        {
            // Kill marker: a quick X at the crosshair.
            var k = (Time.time - _killAt) / 0.4f;
            _kill.gameObject.SetActive(k < 1f);
            if (k < 1f)
                _kill.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, Mathf.Min(1f, k * 3f)) * (1f - 0.2f * k);

            _notice.color = new Color(Accent.r, Accent.g, Accent.b, Mathf.Clamp01((_noticeUntil - Time.time) / 0.4f));

            // Banner: fades in, holds, fades out.
            var b = Time.time - _bannerAt;
            var bannerAlpha = Mathf.Clamp01(b / 0.25f) * Mathf.Clamp01((3.5f - b) / 0.6f);
            _bannerTitle.color = new Color(1, 1, 1, bannerAlpha);
            _bannerSub.color = new Color(Accent.r, Accent.g, Accent.b, bannerAlpha);
        }

        private void updateOverlays(bool dead)
        {
            var flashTime = _player != null ? _player.DamageFlashTime : 0.35f;
            var flash = Mathf.Clamp01(1f - (Time.time - lastDamage) / Mathf.Max(0.01f, flashTime));
            _flash.color = new Color(Danger.r, 0f, 0f, 0.22f * flash);

            var fraction = _health != null ? _health.Health / Mathf.Max(1f, _health.MaxHealth) : 1f;
            var pulse = !dead && fraction <= CriticalHealth ? 0.45f + 0.3f * Mathf.Sin(Time.time * 5f) : 0f;
            _vignette.color = new Color(Danger.r, 0.02f, 0.02f, Mathf.Max(pulse, 0.55f * flash));

            if (dead && _deathTime < 0)
                _deathTime = Time.time;
            var deathAlpha = _deathTime < 0 ? 0f : Mathf.Clamp01((Time.time - _deathTime) / 0.8f);
            _deathBackground.color = new Color(0, 0, 0, 0.75f * deathAlpha);
            _death.color = new Color(Danger.r, Danger.g, Danger.b, deathAlpha);
            _deathSub.color = new Color(1, 1, 1, 0.6f * deathAlpha);
        }

        #endregion

        #region Building

        private static Color Accent => VantageUI.Accent;
        private static Color Danger => VantageUI.Danger;
        private static Color Muted => VantageUI.Muted;
        private static Color Faint => VantageUI.Faint;

        private void build()
        {
            _canvas = VantageUI.Canvas("VANTAGE HUD", 50);
            _vignette = VantageUI.Image(VantageUI.Stretch("Low Health Vignette", _canvas), Color.clear, VantageUI.Vignette);
            _flash = VantageUI.Image(VantageUI.Stretch("Damage Flash", _canvas), Color.clear);
            _hud = VantageUI.Stretch("HUD", _canvas);

            // Soft shading behind the corner clusters keeps them readable on bright walls and sky.
            VantageUI.Image(VantageUI.Rect("Shade", _hud, Vector2.zero, new Vector2(-260, -170), new Vector2(980, 440)), new Color(0, 0, 0, 0.4f), VantageUI.Shade);
            VantageUI.Image(VantageUI.Rect("Shade", _hud, new Vector2(1, 0), new Vector2(260, -170), new Vector2(980, 440)), new Color(0, 0, 0, 0.4f), VantageUI.Shade);
            VantageUI.Image(VantageUI.Rect("Shade", _hud, new Vector2(0.5f, 1), new Vector2(0, 150), new Vector2(1100, 420)), new Color(0, 0, 0, 0.3f), VantageUI.Shade);

            // Health, bottom-left.
            var bl = Vector2.zero;
            _hp = VantageUI.Text(_hud, "Health", 64, TextAlignmentOptions.BottomLeft, bl, new Vector2(Margin, Margin + 10), new Vector2(200, 70));
            _hpFill = VantageUI.Bar(_hud, "Health Bar", bl, new Vector2(Margin, Margin), new Vector2(320, 6), Color.white, out _hpTrail);
            _regenCap = VantageUI.Image(VantageUI.Rect("Regen Limit", _hpFill.transform.parent, new Vector2(0.6f, 0.5f), Vector2.zero, new Vector2(2, 14)), Muted);
            _regenCap.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // Weapon, bottom-right.
            var br = new Vector2(1, 0);
            _reserve = VantageUI.Text(_hud, "Reserve", 30, TextAlignmentOptions.BottomLeft, br, new Vector2(-Margin, Margin + 4), new Vector2(90, 40));
            _reserve.color = Muted;
            _ammo = VantageUI.Text(_hud, "Ammo", 76, TextAlignmentOptions.BottomRight, br, new Vector2(-Margin - 96, Margin - 6), new Vector2(200, 90));
            _weapon = VantageUI.Text(_hud, "Weapon", 26, TextAlignmentOptions.BottomRight, br, new Vector2(-Margin, Margin + 84), new Vector2(500, 32));

            // Level, top-centre.
            var tc = new Vector2(0.5f, 1);
            _levelNumber = VantageUI.Text(_hud, "Level Number", 24, TextAlignmentOptions.Top, tc, new Vector2(0, -34), new Vector2(600, 30));
            _levelNumber.color = Accent;
            _levelName = VantageUI.Text(_hud, "Level Name", 40, TextAlignmentOptions.Top, tc, new Vector2(0, -58), new Vector2(900, 46));
            for (int i = 0; i < MaxPips; i++)
            {
                _pips[i] = VantageUI.Image(VantageUI.Rect("Enemy", _hud, tc, Vector2.zero, new Vector2(9, 9)), Faint, VantageUI.Pixel);
                _pips[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _pips[i].rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            }
            _hint = VantageUI.Text(_hud, "Hint", 24, TextAlignmentOptions.Top, tc, new Vector2(0, -144), new Vector2(600, 30));
            _hint.color = Accent;

            // Centre.
            var c = new Vector2(0.5f, 0.5f);
            _target = VantageUI.Rect("Target", _hud, c, new Vector2(0, -46), new Vector2(110, 4));
            _targetFill = VantageUI.Bar(_target, "Target Health", c, Vector2.zero, new Vector2(110, 4), Danger, out _targetTrail);
            _kill = VantageUI.Rect("Kill Marker", _hud, c, Vector2.zero, new Vector2(30, 30));
            foreach (var angle in new[] { 45f, -45f })
                VantageUI.Image(VantageUI.Rect("Stroke", _kill, c, Vector2.zero, new Vector2(3, 30)), Danger, VantageUI.Pixel).rectTransform.localEulerAngles = new Vector3(0, 0, angle);
            _notice = VantageUI.Text(_hud, "Pickup", 34, TextAlignmentOptions.Center, new Vector2(0.5f, 0.36f), Vector2.zero, new Vector2(900, 50));
            _notice.color = Color.clear;
            _bannerTitle = VantageUI.Text(_hud, "Banner", 72, TextAlignmentOptions.Center, new Vector2(0.5f, 0.7f), Vector2.zero, new Vector2(1400, 90));
            _bannerSub = VantageUI.Text(_hud, "Banner Detail", 28, TextAlignmentOptions.Center, new Vector2(0.5f, 0.7f), new Vector2(0, -62), new Vector2(1400, 40));
            _bannerTitle.color = _bannerSub.color = Color.clear;

            // Death screen.
            _deathBackground = VantageUI.Image(VantageUI.Stretch("Death Background", _canvas), Color.clear);
            _death = VantageUI.Text(_canvas, "Death", 120, TextAlignmentOptions.Center, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(1400, 140));
            _death.text = VantageUI.Caps("Signal lost");
            _deathSub = VantageUI.Text(_canvas, "Death Detail", 30, TextAlignmentOptions.Center, new Vector2(0.5f, 0.55f), new Vector2(0, -96), new Vector2(1400, 40));
            _deathSub.text = "RESTARTING";
            _death.color = _deathSub.color = Color.clear;
        }

        #endregion
    }
}
