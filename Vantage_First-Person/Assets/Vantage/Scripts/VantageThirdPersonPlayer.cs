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

        [Header("Health")]
        [Tooltip("The template character has 400 HP and regenerates 5 HP per second, more than a drone deals, so he could never die. This replaces it.")]
        public float MaxHealth = 100f;
        [Tooltip("Health regenerated per second, but only after a few seconds without taking damage: staying in cover pays off, standing in fire does not.")]
        public float RegenPerSecond = 4f;
        public float RegenDelay = 6f;
        [Tooltip("Regeneration stops at this fraction of max health, so damage taken still matters later.")]
        [Range(0, 1)] public float RegenCap = 0.6f;

        private CharacterMotor _motor;
        private CharacterHealth _health;
        private float _previousHealth;
        private float _lastDamage = -100f;
        private bool _dead;

        /// <summary>
        /// Time the player last lost health, for the HUD.
        /// </summary>
        public float LastDamageTime => _lastDamage;

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _health = GetComponent<CharacterHealth>();
            var actor = GetComponent<BaseActor>();
            if (actor != null)
                actor.Side = Side;
            if (_health != null)
            {
                _health.MaxHealth = MaxHealth;
                _health.Health = MaxHealth;
                _health.Regeneration = 0f;
                _health.IsTakingDamage = true;
                _previousHealth = _health.Health;
            }

            if (GetComponent<VantagePlayerHUD>() == null)
                gameObject.AddComponent<VantagePlayerHUD>();
        }

        private void Update()
        {
            if (_health != null)
            {
                if (_health.Health < _previousHealth)
                    _lastDamage = Time.time;

                if (!_dead && _motor.IsAlive && Time.time - _lastDamage > RegenDelay && _health.Health < MaxHealth * RegenCap)
                    _health.Health = Mathf.Min(MaxHealth * RegenCap, _health.Health + RegenPerSecond * Time.deltaTime);

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

        /// <summary>
        /// True once the player has died and the restart is pending. The damage flash and death screen are drawn by VantagePlayerHUD.
        /// </summary>
        public bool IsDead => _dead;
    }
}
