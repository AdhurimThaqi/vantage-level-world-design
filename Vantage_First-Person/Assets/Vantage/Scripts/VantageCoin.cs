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
            VantageAudio.Play(VantageSounds.CoinCollect, transform.position, PickupSound);   // empty and no clip: silent
            VantageEvents.RaiseCoinCollected(VantageRun.Coins.Count, Total);
            gameObject.SetActive(false);
        }
    }
}
