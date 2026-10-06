using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Minimal health, ammo and crosshair readout drawn with IMGUI, so it needs no Canvas setup.
    /// Swap for a uGUI canvas later if you want it styled.
    /// </summary>
    [RequireComponent(typeof(VantagePlayer))]
    public class VantageHUD : MonoBehaviour
    {
        public Color HealthColor = new Color(0.85f, 0.2f, 0.2f);
        public Color TextColor = Color.white;
        public float DamageFlashTime = 0.35f;

        private VantagePlayer _player;
        private VantageWeapons _weapons;
        private GUIStyle _big;
        private GUIStyle _small;
        private Texture2D _pixel;

        private void Awake()
        {
            _player = GetComponent<VantagePlayer>();
            _weapons = GetComponentInChildren<VantageWeapons>();
            _pixel = Texture2D.whiteTexture;
        }

        private void OnGUI()
        {
            if (_big == null)
            {
                _big = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
                _small = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.LowerLeft };
                _big.normal.textColor = TextColor;
                _small.normal.textColor = TextColor;
            }

            var w = Screen.width;
            var h = Screen.height;

            // Red flash when hurt.
            var flash = 1f - (Time.time - _player.LastDamageTime) / DamageFlashTime;
            if (flash > 0)
                drawRect(new Rect(0, 0, w, h), new Color(0.6f, 0, 0, 0.35f * flash));

            if (_player.IsDead)
            {
                var dead = new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter, fontSize = 48 };
                GUI.Label(new Rect(0, 0, w, h), "YOU DIED", dead);
                return;
            }

            // Crosshair.
            if (_weapons != null && _weapons.Current != null)
            {
                drawRect(new Rect(w / 2 - 1, h / 2 - 8, 2, 16), TextColor);
                drawRect(new Rect(w / 2 - 8, h / 2 - 1, 16, 2), TextColor);
            }

            // Health bar.
            var health = _player.Health;
            var fraction = health.MaxHealth > 0 ? health.Health / health.MaxHealth : 0;
            drawRect(new Rect(24, h - 44, 260, 20), new Color(0, 0, 0, 0.5f));
            drawRect(new Rect(26, h - 42, 256 * fraction, 16), HealthColor);
            GUI.Label(new Rect(24, h - 72, 260, 24), $"HEALTH  {Mathf.CeilToInt(health.Health)}", _small);

            // Ammo.
            if (_weapons != null && _weapons.Current != null)
            {
                var weapon = _weapons.Current;
                var ammo = _weapons.IsReloading ? "RELOADING" : $"{weapon.Ammo} / {weapon.ReserveAmmo}";
                GUI.Label(new Rect(w - 324, h - 64, 300, 40), ammo, _big);
                GUI.Label(new Rect(w - 324, h - 92, 300, 24), weapon.Name.ToUpper(), new GUIStyle(_small) { alignment = TextAnchor.LowerRight });
            }
        }

        private void drawRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
        }
    }
}
