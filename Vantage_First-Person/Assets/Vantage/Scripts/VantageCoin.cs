using System.Collections.Generic;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// A gold coin hidden off the main route (one or two per level): it turns and bobs so it catches the eye from a
    /// doorway, and the player collects it by walking into it. Collected coins stay collected after a death
    /// (VantageRun) and are counted on the HUD and the end screen.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class VantageCoin : MonoBehaviour
    {
        [Tooltip("Stable name of this coin, e.g. coin_L1_RoomC (kept across the reload after a death).")]
        public string Id;
        public float SpinSpeed = 120f;
        public float BobHeight = 0.06f;
        public AudioClip PickupSound;

        private static readonly List<VantageCoin> all = new List<VantageCoin>();
        /// <summary>Coins in the level, collected or not.</summary>
        public static int Total => all.Count;

        private Vector3 _home;
        private static AudioClip _chime;

        /// <summary>A bright two-note chime, synthesised once (no audio file needed).</summary>
        private static AudioClip chime()
        {
            if (_chime != null) return _chime;
            const int rate = 22050;
            var data = new float[(int)(rate * 0.45f)];
            for (int i = 0; i < data.Length; i++)
            {
                var t = i / (float)rate;
                var note = t < 0.09f ? 1318.5f : 1975.5f;            // E6, then B6
                var start = t < 0.09f ? 0f : 0.09f;
                var envelope = Mathf.Exp(-(t - start) * 9f);
                data[i] = (Mathf.Sin(t * note * Mathf.PI * 2f) * 0.6f + Mathf.Sin(t * note * 2f * Mathf.PI * 2f) * 0.15f) * envelope;
            }
            _chime = AudioClip.Create("Coin", data.Length, 1, rate, false);
            _chime.SetData(data, 0);
            return _chime;
        }

        private void Awake()
        {
            all.Add(this);
            _home = transform.position;
            GetComponent<SphereCollider>().isTrigger = true;
        }

        private void OnDestroy() => all.Remove(this);

        private void Start()
        {
            if (VantageRun.Coins.Contains(Id)) gameObject.SetActive(false);
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
            transform.position = _home + Vector3.up * (Mathf.Sin(Time.time * 2.4f) * BobHeight);
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = VantageEvents.ActivePlayer();
            if (player == null || other.GetComponentInParent<CoverShooter.BaseActor>() != player) return;
            Collect();
        }

        /// <summary>Collects the coin (walking into it, or the autoplay bot).</summary>
        public void Collect()
        {
            if (!gameObject.activeSelf || !VantageRun.Coins.Add(Id)) return;
            AudioSource.PlayClipAtPoint(PickupSound != null ? PickupSound : chime(), transform.position, 0.8f);
            VantageEvents.RaiseCoinCollected(VantageRun.Coins.Count, Total);
            gameObject.SetActive(false);
        }
    }
}
