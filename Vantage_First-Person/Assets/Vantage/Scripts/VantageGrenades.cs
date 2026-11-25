using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The player's grenades (2026-11-25). The template lets the rig throw grenades without limit from the start and
    /// nothing in the level ever showed it; now they are found (the grenade bucket in the L3 robot room,
    /// VantageGrenadeCrate) and counted: G only takes one out while the player has one (the template input asks
    /// CanTakeGrenade), and every throw takes one. Until the first throw the HUD shows how (Hint).
    /// Added to the player by VantageThirdPersonPlayer. The count lives in VantageRun (kept after a death).
    /// </summary>
    public class VantageGrenades : MonoBehaviour
    {
        public const string HowTo = "Hold G to aim a grenade, left click to throw";

        public static VantageGrenades Instance { get; private set; }
        public static int Count => VantageRun.Grenades;
        /// <summary>The how-to line the HUD shows (null: none): from the first pickup until the first throw.</summary>
        public static string Hint => VantageRun.Grenades > 0 && !VantageRun.GrenadeTaught ? HowTo : null;

        private CharacterMotor _motor;
        private bool _wasThrowing;

        private void Awake()
        {
            Instance = this;
            _motor = GetComponent<CharacterMotor>();
            ThirdPersonInput.CanTakeGrenade = () => VantageRun.Grenades > 0;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Fills the player up to this many grenades; false when already that many (the bucket stays).</summary>
        public static bool FillTo(int count)
        {
            if (VantageRun.Grenades >= count) return false;
            VantageRun.Grenades = count;
            return true;
        }

        private void Update()
        {
            if (_motor == null) return;
            // One grenade per throw, counted as the throw starts.
            var throwing = _motor.IsThrowingGrenade;
            if (throwing && !_wasThrowing && VantageRun.Grenades > 0)
            {
                VantageRun.Grenades--;
                VantageRun.GrenadeTaught = true;
            }
            _wasThrowing = throwing;
            // Out of grenades with one out in the hand (taken just as the last left): put it away.
            if (VantageRun.Grenades <= 0 && _motor.HasGrenadeInHand && !throwing)
                _motor.InputCancelGrenade();
        }
    }
}
