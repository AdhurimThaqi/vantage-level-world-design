using System.Collections.Generic;
using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Makes the Cover Shooter character start unarmed (concept doc: the pistol is found, then the rifle).
    /// Weapons stay in the character's inventory data but are hidden and unusable until a WeaponPickup unlocks them.
    /// Weapon names are the names of their RightItem objects (Pistol, Rifle, Sniper).
    /// Runs before the template's scripts so they never see the locked weapons.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(CharacterInventory))]
    public class VantageArsenal : MonoBehaviour
    {
        [Tooltip("Weapons available from the start. Empty = start unarmed.")]
        public List<string> StartUnlocked = new List<string>();

        private CharacterInventory _inventory;
        private CharacterMotor _motor;
        private ThirdPersonController _controller;
        private WeaponDescription[] _all;
        private readonly List<string> _unlocked = new List<string>();

        private void Awake()
        {
            _inventory = GetComponent<CharacterInventory>();
            _motor = GetComponent<CharacterMotor>();
            _controller = GetComponent<ThirdPersonController>();
            _all = _inventory.Weapons;
            _unlocked.AddRange(StartUnlocked);

            if (!isUnlocked(_motor.Weapon))
            {
                _motor.IsEquipped = false;
                _motor.Weapon = WeaponDescription.Default();
            }

            refresh();
        }

        /// <summary>
        /// The player's own sounds from the sound bank, where they have clips: GUN_Pistol on the pistol, GUN_Empty on
        /// every gun's empty click, PLAYER_Jump on the jump. The template plays them (through VantageAudio.PlayClip);
        /// an empty event keeps the template's clip. In Start: the bank's VantageAudio wakes in Awake.
        /// </summary>
        private void Start()
        {
            var pistol = VantageAudio.Clips(VantageSounds.GunPistol);
            var empty = VantageAudio.Clips(VantageSounds.GunEmpty);
            foreach (var weapon in _all)
            {
                if (weapon.RightItem == null) continue;
                foreach (var sounds in weapon.RightItem.GetComponentsInChildren<GunSounds>(true))
                {
                    if (pistol != null && weapon.RightItem.name == "Pistol") sounds.Fire = pistol;
                    if (empty != null) sounds.EmptyFire = empty;
                }
            }
            var jump = VantageAudio.Clips(VantageSounds.PlayerJump);
            var character = GetComponent<CharacterSounds>();
            if (jump != null && character != null) character.Jump = jump;
        }

        /// <summary>Whether the player has picked up this gun (its RightItem name, e.g. "Rifle").</summary>
        public bool IsUnlocked(string weaponName) => _unlocked.Contains(weaponName);

        public bool Unlock(string weaponName)
        {
            var index = System.Array.FindIndex(_all, w => w.RightItem != null && w.RightItem.name == weaponName);
            if (index < 0)
            {
                Debug.LogWarning($"[Vantage] The character has no weapon called '{weaponName}'.");
                return false;
            }

            if (!_unlocked.Contains(weaponName))
                _unlocked.Add(weaponName);

            refresh();

            if (_controller != null)
                _controller.InputEquip(_all[index]);

            return true;
        }

        /// <summary>Puts an unlocked weapon straight into the hands, without the equip animation (checkpoint restart).</summary>
        public void EquipNow(string weaponName)
        {
            var index = System.Array.FindIndex(_all, w => w.RightItem != null && w.RightItem.name == weaponName);
            if (index < 0 || !_unlocked.Contains(weaponName)) return;
            _motor.Weapon = _all[index];
            _motor.IsEquipped = true;
            if (_controller != null) _controller.InputEquip(_all[index]);
            setActive(_all[index].RightItem, true);
            setActive(_all[index].LeftItem, true);
            refresh();
        }

        /// <summary>
        /// The inventory only lists unlocked weapons; locked ones are hidden, holster included.
        /// </summary>
        private void refresh()
        {
            var available = new List<WeaponDescription>();

            foreach (var weapon in _all)
            {
                var open = isUnlocked(weapon);
                if (open)
                    available.Add(weapon);

                var equipped = _motor.IsEquipped && weapon.IsTheSame(ref _motor.Weapon);
                setActive(weapon.RightHolster, open && !equipped);
                setActive(weapon.LeftHolster, open && !equipped);
                if (!open)
                {
                    setActive(weapon.RightItem, false);
                    setActive(weapon.LeftItem, false);
                    setActive(weapon.Shield, false);
                }
            }

            _inventory.Weapons = available.ToArray();
        }

        private bool isUnlocked(WeaponDescription weapon)
        {
            return weapon.RightItem != null && _unlocked.Contains(weapon.RightItem.name);
        }

        private static void setActive(GameObject go, bool value)
        {
            if (go != null && go.activeSelf != value)
                go.SetActive(value);
        }
    }
}
