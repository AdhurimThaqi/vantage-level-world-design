using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Unlocks a weapon in the player's VantageArsenal when the player walks into it.
    /// Uses a distance check, so it needs no collider or rigidbody.
    /// </summary>
    public class WeaponPickup : MonoBehaviour
    {
        [Tooltip("Must match a weapon in the player's inventory (e.g. Pistol, Rifle).")]
        public string WeaponName = "Rifle";

        public int ExtraAmmo = 30;
        public float PickupRadius = 1.5f;

        [Tooltip("Degrees per second. 0 keeps the pickup still, e.g. lying next to a body.")]
        public float SpinSpeed = 45f;

        public AudioClip PickupSound;

        private void Update()
        {
            if (SpinSpeed != 0)
                transform.Rotate(0, SpinSpeed * Time.deltaTime, 0, Space.World);

            var player = VantageEvents.ActivePlayer();
            if (player == null)
                return;

            var center = player.transform.position + Vector3.up;
            if (Vector3.Distance(center, transform.position) > PickupRadius)
                return;

            var arsenal = player.GetComponent<VantageArsenal>();
            if (arsenal == null || !arsenal.Unlock(WeaponName))
                return;

            VantageEvents.RaiseWeaponPickedUp(WeaponName);

            if (PickupSound != null)
                AudioSource.PlayClipAtPoint(PickupSound, transform.position);

            Destroy(gameObject);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, PickupRadius);
        }
    }
}
