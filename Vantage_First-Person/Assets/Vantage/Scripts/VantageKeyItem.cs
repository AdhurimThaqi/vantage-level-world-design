using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The level 2 key (L2_PICKUP_Key): floats and turns on the desk with a small light so it reads from the door;
    /// when the player takes it (VantagePickup) it flies into the player's hands, shrinking, and is gone.
    /// </summary>
    public class VantageKeyItem : MonoBehaviour
    {
        public float FloatHeight = 0.08f;
        public float FloatSpeed = 1f;
        public float SpinSpeed = 90f;
        public float FlyTime = 0.35f;

        private Vector3 _home, _scale, _flyFrom;
        private float _flyStart = -1f;
        private Light _light;
        private VantagePickup _pickup;

        private void Awake()
        {
            _pickup = GetComponent<VantagePickup>();
            _home = transform.position;
            _scale = transform.localScale;
            _light = GetComponentInChildren<Light>();
        }

        private void OnEnable() => VantageEvents.ItemPickedUp += onPicked;
        private void OnDisable() => VantageEvents.ItemPickedUp -= onPicked;

        private void onPicked(string item)
        {
            if (_flyStart >= 0 || _pickup == null || item != _pickup.item) return;
            _flyStart = Time.time;
            _flyFrom = transform.position;
            if (_light != null) _light.enabled = false;
            VantageEvents.RaiseMessage(item + " acquired");
        }

        private void Update()
        {
            if (_flyStart < 0)
            {
                transform.position = _home + Vector3.up * (Mathf.Sin(Time.time * FloatSpeed * Mathf.PI * 2f) * FloatHeight);
                transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
                return;
            }
            var t = Mathf.Clamp01((Time.time - _flyStart) / FlyTime);
            var player = VantageEvents.ActivePlayer();
            var to = player != null ? VantagePhysics.AimPoint(player) : _flyFrom;
            // Up in an arc, then into the hands.
            transform.position = Vector3.Lerp(_flyFrom, to, t * t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.4f;
            transform.localScale = _scale * (1f - t * 0.8f);
            transform.Rotate(Vector3.up, SpinSpeed * 4f * Time.deltaTime, Space.World);
            if (t >= 1f) gameObject.SetActive(false);
        }
    }
}
