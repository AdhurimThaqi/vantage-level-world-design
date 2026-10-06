using System.Collections.Generic;
using CoverShooter;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vantage
{
    [System.Serializable]
    public class WeaponSlot
    {
        public string Name = "Pistol";

        [Tooltip("Locked weapons are unlocked by a WeaponPickup.")]
        public bool Unlocked = true;

        [Tooltip("Hold to fire continuously.")]
        public bool Automatic;

        public float Damage = 20f;

        [Tooltip("Shots per second.")]
        public float FireRate = 4f;

        public int MagazineSize = 12;
        public int ReserveAmmo = 48;
        public float ReloadTime = 1.2f;
        public float Range = 150f;

        [Tooltip("Random spread cone in degrees.")]
        public float Spread = 0.5f;

        [Tooltip("Tells Cover Shooter characters which hit reaction to play.")]
        public HitType HitType = HitType.Pistol;

        [Header("Visuals")]
        [Tooltip("First-person model, a child of the camera root. Shown only while equipped.")]
        public GameObject Model;
        public Transform Muzzle;
        public GameObject MuzzleFlash;
        public AudioClip[] FireSounds;
        public AudioClip ReloadSound;

        [HideInInspector] public int Ammo = -1;
    }

    /// <summary>
    /// First-person hitscan weapons. Hits go through Cover Shooter's own "OnHit" message,
    /// so its enemies react, bleed, take cover and die as they do in the template.
    /// Fire: left mouse. Reload: R. Switch: 1-9 or the mouse wheel.
    /// </summary>
    public class VantageWeapons : MonoBehaviour
    {
        public List<WeaponSlot> Weapons = new List<WeaponSlot>();

        [Tooltip("Spawned where a bullet hits the level. Enemies play their own blood effect.")]
        public GameObject ImpactEffect;

        [Tooltip("What bullets can hit.")]
        public LayerMask HitMask = ~((1 << 2) | (1 << 5) | (1 << 11)); // not Ignore Raycast, UI, Zones

        [Tooltip("Enemies within this distance hear each shot and come looking.")]
        public float AlertRange = 25f;

        [Tooltip("How far the model kicks back per shot.")]
        public float RecoilKick = 0.04f;

        public int CurrentIndex { get; private set; } = -1;
        public WeaponSlot Current => CurrentIndex >= 0 ? Weapons[CurrentIndex] : null;
        public bool IsReloading => _reloadEnd > Time.time;

        private VantagePlayer _player;
        private AudioSource _audio;
        private float _nextShot;
        private float _reloadEnd;
        private readonly Dictionary<GameObject, Vector3> _modelRest = new Dictionary<GameObject, Vector3>();
        private readonly RaycastHit[] _hits = new RaycastHit[32];

        private void Awake()
        {
            _player = GetComponentInParent<VantagePlayer>();
            _audio = GetComponent<AudioSource>();
            if (_audio == null)
                _audio = gameObject.AddComponent<AudioSource>();

            foreach (var weapon in Weapons)
            {
                if (weapon.Ammo < 0)
                    weapon.Ammo = weapon.MagazineSize;

                if (weapon.Model != null)
                {
                    _modelRest[weapon.Model] = weapon.Model.transform.localPosition;
                    weapon.Model.SetActive(false);
                }
            }

            for (int i = 0; i < Weapons.Count; i++)
                if (Weapons[i].Unlocked)
                {
                    Equip(i);
                    break;
                }
        }

        private void Update()
        {
            if (_player != null && _player.IsDead)
                return;

            readSwitchInput();

            var weapon = Current;
            if (weapon == null)
                return;

            if (weapon.Model != null)
            {
                var rest = _modelRest[weapon.Model];
                weapon.Model.transform.localPosition = Vector3.Lerp(weapon.Model.transform.localPosition, rest, Time.deltaTime * 12f);
            }

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                Reload();

            if (mouse == null || IsReloading)
                return;

            var trigger = weapon.Automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;

            if (trigger && Time.time >= _nextShot)
            {
                if (weapon.Ammo > 0)
                    fire(weapon);
                else
                    Reload();
            }
        }

        public void Equip(int index)
        {
            if (index < 0 || index >= Weapons.Count || !Weapons[index].Unlocked || index == CurrentIndex)
                return;

            if (Current != null && Current.Model != null)
                Current.Model.SetActive(false);

            CurrentIndex = index;
            _reloadEnd = 0;

            if (Current.Model != null)
                Current.Model.SetActive(true);
        }

        /// <summary>
        /// Unlocks the weapon with the given name, adds ammo and equips it. Used by WeaponPickup.
        /// </summary>
        public bool Unlock(string weaponName, int extraAmmo)
        {
            var index = Weapons.FindIndex(w => w.Name == weaponName);
            if (index < 0)
            {
                Debug.LogWarning($"VantageWeapons has no weapon named '{weaponName}'.");
                return false;
            }

            Weapons[index].Unlocked = true;
            Weapons[index].ReserveAmmo += extraAmmo;
            Equip(index);
            return true;
        }

        public void Reload()
        {
            var weapon = Current;
            if (weapon == null || IsReloading || weapon.Ammo >= weapon.MagazineSize || weapon.ReserveAmmo <= 0)
                return;

            _reloadEnd = Time.time + weapon.ReloadTime;
            if (weapon.ReloadSound != null)
                _audio.PlayOneShot(weapon.ReloadSound);

            var taken = Mathf.Min(weapon.MagazineSize - weapon.Ammo, weapon.ReserveAmmo);
            weapon.ReserveAmmo -= taken;
            weapon.Ammo += taken;
        }

        private void readSwitchInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
                for (int i = 0; i < Weapons.Count && i < 9; i++)
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                        Equip(i);

            var mouse = Mouse.current;
            if (mouse == null || CurrentIndex < 0)
                return;

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f)
                return;

            var step = scroll > 0 ? -1 : 1;
            for (int i = 1; i < Weapons.Count; i++)
            {
                var next = (CurrentIndex + step * i + Weapons.Count) % Weapons.Count;
                if (Weapons[next].Unlocked)
                {
                    Equip(next);
                    break;
                }
            }
        }

        private void fire(WeaponSlot weapon)
        {
            _nextShot = Time.time + 1f / Mathf.Max(0.01f, weapon.FireRate);
            weapon.Ammo--;

            var view = Camera.main != null ? Camera.main.transform : transform;
            var spread = Random.insideUnitCircle * weapon.Spread;
            var direction = Quaternion.AngleAxis(spread.x, view.up) * Quaternion.AngleAxis(spread.y, view.right) * view.forward;

            if (findHit(view.position, direction, weapon.Range, out var hit))
            {
                var attacker = _player != null ? _player.gameObject : gameObject;
                var target = hit.collider.gameObject;
                var hitInfo = new Hit(hit.point, -direction, weapon.Damage, attacker, target, weapon.HitType, 0);

                target.SendMessage("OnHit", hitInfo, SendMessageOptions.DontRequireReceiver);

                var isCharacter = BodyPartHealth.Contains(target) || CharacterHealth.Get(target) != null;
                if (!isCharacter && ImpactEffect != null)
                    Destroy(Instantiate(ImpactEffect, hit.point, Quaternion.LookRotation(hit.normal)), 5f);
            }

            if (_player != null)
                Alerts.Broadcast(view.position, AlertRange, AlertType.GunFire, _player.Actor, true);

            if (weapon.MuzzleFlash != null)
            {
                var muzzle = weapon.Muzzle != null ? weapon.Muzzle : view;
                Destroy(Instantiate(weapon.MuzzleFlash, muzzle.position, muzzle.rotation, muzzle), 2f);
            }

            if (weapon.FireSounds != null && weapon.FireSounds.Length > 0)
                _audio.PlayOneShot(weapon.FireSounds[Random.Range(0, weapon.FireSounds.Length)]);

            if (weapon.Model != null)
                weapon.Model.transform.localPosition -= Vector3.forward * RecoilKick;
        }

        /// <summary>
        /// Closest valid hit, skipping the player itself and triggers that are not character hitboxes.
        /// </summary>
        private bool findHit(Vector3 origin, Vector3 direction, float range, out RaycastHit closest)
        {
            closest = default;
            var found = false;
            var count = Physics.RaycastNonAlloc(origin, direction, _hits, range, HitMask, QueryTriggerInteraction.Collide);
            var self = _player != null ? _player.transform : transform;

            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];

                if (hit.collider.transform.IsChildOf(self))
                    continue;

                if (hit.collider.isTrigger && !BodyPartHealth.Contains(hit.collider.gameObject))
                    continue;

                if (!found || hit.distance < closest.distance)
                {
                    closest = hit;
                    found = true;
                }
            }

            return found;
        }
    }
}
