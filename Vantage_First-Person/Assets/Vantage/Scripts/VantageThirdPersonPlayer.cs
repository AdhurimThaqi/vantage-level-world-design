using CoverShooter;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vantage
{
    /// <summary>
    /// Player-side rules for the Cover Shooter character: AI team, damage flash, death and level restart.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class VantageThirdPersonPlayer : MonoBehaviour
    {
        [Tooltip("AI team. Enemies use side 0.")]
        public int Side = 1;
        public float RestartDelay = 3f;
        public float DamageFlashTime = 0.35f;

        private CharacterMotor _motor;
        private CharacterHealth _health;
        private float _previousHealth;
        private float _lastDamage = -100f;
        private bool _dead;

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _health = GetComponent<CharacterHealth>();
            var actor = GetComponent<BaseActor>();
            if (actor != null)
                actor.Side = Side;
            if (_health != null)
                _previousHealth = _health.Health;
        }

        private void Update()
        {
            if (_health != null)
            {
                if (_health.Health < _previousHealth)
                    _lastDamage = Time.time;
                _previousHealth = _health.Health;
            }

            if (!_dead && !_motor.IsAlive)
            {
                _dead = true;
                VantageEvents.RaisePlayerDied("health reached zero", transform.position);
                Invoke(nameof(restart), RestartDelay);
            }
        }

        private void restart()
        {
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        private void OnGUI()
        {
            var flash = 1f - (Time.time - _lastDamage) / DamageFlashTime;
            if (flash > 0)
            {
                GUI.color = new Color(0.6f, 0, 0, 0.3f * flash);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (_dead)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = Color.white;
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "YOU DIED", style);
            }
        }
    }
}
