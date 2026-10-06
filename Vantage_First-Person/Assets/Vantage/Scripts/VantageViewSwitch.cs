using CoverShooter;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vantage
{
    /// <summary>
    /// Swaps between the first-person player (Starter Assets + VantagePlayer) and the
    /// Cover Shooter third-person character. Position, facing and health carry over.
    /// Only the active one exists for the AI, so enemies always target the one you control.
    /// </summary>
    public class VantageViewSwitch : MonoBehaviour
    {
        public Key SwitchKey = Key.V;
        public bool StartInThirdPerson;

        [Header("First person")]
        public VantagePlayer FirstPersonPlayer;
        [Tooltip("Starter Assets MainCamera and PlayerFollowCamera.")]
        public GameObject[] FirstPersonCameras;

        [Header("Third person")]
        [Tooltip("Root holding the Cover Shooter character, its camera and its HUD.")]
        public GameObject ThirdPersonRig;
        public CharacterMotor ThirdPersonCharacter;

        public bool IsThirdPerson { get; private set; }

        private bool _restarting;

        private void Start()
        {
            setThirdPerson(StartInThirdPerson, false);
        }

        private void Update()
        {
            if (IsThirdPerson && !_restarting && ThirdPersonCharacter != null && !ThirdPersonCharacter.IsAlive)
            {
                _restarting = true;
                VantageEvents.RaisePlayerDied("health reached zero (third person)", ThirdPersonCharacter.transform.position);
                Invoke(nameof(restart), FirstPersonPlayer != null ? FirstPersonPlayer.RestartDelay : 3f);
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[SwitchKey].wasPressedThisFrame || _restarting)
                return;

            if (FirstPersonPlayer != null && FirstPersonPlayer.IsDead)
                return;

            setThirdPerson(!IsThirdPerson, true);
        }

        private void setThirdPerson(bool value, bool carryOver)
        {
            if (FirstPersonPlayer == null || ThirdPersonRig == null || ThirdPersonCharacter == null)
            {
                Debug.LogWarning("[Vantage] View switch is missing a reference.");
                return;
            }

            var fps = FirstPersonPlayer.transform;
            var tps = ThirdPersonCharacter.transform;
            var fpsHealth = FirstPersonPlayer.GetComponent<CharacterHealth>();
            var tpsHealth = ThirdPersonCharacter.GetComponent<CharacterHealth>();

            if (carryOver)
            {
                var from = value ? fps : tps;
                var to = value ? tps : fps;
                var yaw = Quaternion.Euler(0, from.eulerAngles.y, 0);

                // Both objects are inactive at this point on the receiving side, so moving them is safe.
                to.SetPositionAndRotation(from.position, yaw);

                var body = tps.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic)
                    body.linearVelocity = Vector3.zero;

                if (fpsHealth != null && tpsHealth != null)
                {
                    if (value)
                        tpsHealth.Health = fpsHealth.Health;
                    else
                        fpsHealth.Health = tpsHealth.Health;
                }
            }

            // Deactivate the current side first so two cameras or audio listeners never overlap.
            if (value)
            {
                setFirstPersonActive(false);
                ThirdPersonRig.SetActive(true);
            }
            else
            {
                ThirdPersonRig.SetActive(false);
                setFirstPersonActive(true);
            }

            IsThirdPerson = value;
            if (carryOver)
                VantageEvents.RaiseViewSwitched(value);
        }

        private void setFirstPersonActive(bool active)
        {
            FirstPersonPlayer.gameObject.SetActive(active);

            foreach (var cam in FirstPersonCameras)
                if (cam != null)
                    cam.SetActive(active);
        }

        private void restart()
        {
            VantagePlayer.RestartLevel();
        }
    }
}
