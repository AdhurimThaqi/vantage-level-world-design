using CoverShooter;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vantage
{
    /// <summary>
    /// Connects the Starter Assets first-person player to the Cover Shooter AI and damage system.
    /// BaseActor makes the player visible to enemy AI, CharacterHealth lets their bullets hurt it.
    /// The player object must be on the "Character" layer (10) for the AI to find it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(BaseActor))]
    [RequireComponent(typeof(CharacterHealth))]
    public class VantagePlayer : MonoBehaviour
    {
        public static VantagePlayer Instance { get; private set; }

        [Tooltip("AI team. Cover Shooter enemies use side 0, so the player has to be on a different one.")]
        public int Side = 1;

        [Tooltip("Seconds between death and the level restarting.")]
        public float RestartDelay = 3f;

        public CharacterHealth Health { get; private set; }
        public BaseActor Actor { get; private set; }
        public bool IsDead { get; private set; }
        public float LastDamageTime { get; private set; } = -100f;

        private float _previousHealth;

        private void Awake()
        {
            Instance = this;
            Health = GetComponent<CharacterHealth>();
            Actor = GetComponent<BaseActor>();
            Actor.Side = Side;
            _previousHealth = Health.Health;

            // Bullets can hit colliders below the root (e.g. the Starter Assets capsule mesh).
            // BodyPartHealth forwards those hits up to the root's OnHit.
            foreach (var part in GetComponentsInChildren<Collider>())
            {
                if (part.gameObject == gameObject || part.isTrigger)
                    continue;

                var forwarder = part.GetComponent<BodyPartHealth>();
                if (forwarder == null)
                    forwarder = part.gameObject.AddComponent<BodyPartHealth>();

                forwarder.TargetOveride = Health;
            }
        }

        private void OnEnable()
        {
            Health.Changed += OnHealthChanged;
        }

        private void OnDisable()
        {
            Health.Changed -= OnHealthChanged;
        }

        /// <summary>
        /// Called by Cover Shooter guns, grenades and melee weapons through SendMessage.
        /// </summary>
        public void OnHit(Hit hit)
        {
            if (!IsDead)
                Health.Deal(hit.Damage);
        }

        private void OnHealthChanged(float value)
        {
            if (value < _previousHealth)
                LastDamageTime = Time.time;

            _previousHealth = value;

            if (value <= 0 && !IsDead)
                Die();
        }

        private void Die()
        {
            IsDead = true;
            VantageEvents.RaisePlayerDied("health reached zero (first person)", transform.position);

            // Tells BaseActor (AI stops targeting) and CharacterHealth that the player is dead.
            SendMessage("OnDead", SendMessageOptions.DontRequireReceiver);

            var controller = GetComponent<FirstPersonController>();
            if (controller != null)
                controller.enabled = false;

            Invoke(nameof(restart), RestartDelay);
        }

        private void restart()
        {
            RestartLevel();
        }

        public static void RestartLevel()
        {
            var scene = SceneManager.GetActiveScene();

#if UNITY_EDITOR
            // Works even when the scene is not in the build settings.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }
    }
}
