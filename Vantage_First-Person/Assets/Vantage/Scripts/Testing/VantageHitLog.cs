#if UNITY_EDITOR
using System.Collections.Generic;
using CoverShooter;
using UnityEngine;

namespace Vantage.Testing
{
    /// <summary>Editor-only: records every hit the player takes (attacker, damage, time) for the bot's diagnoses.</summary>
    public class VantageHitLog : MonoBehaviour
    {
        public readonly List<(float time, string attacker, float damage, HitType type)> Hits = new List<(float, string, float, HitType)>();

        private void OnHit(Hit hit) => Hits.Add((Time.time, hit.Attacker != null ? hit.Attacker.name : "?", hit.Damage, hit.Type));
    }
}
#endif
