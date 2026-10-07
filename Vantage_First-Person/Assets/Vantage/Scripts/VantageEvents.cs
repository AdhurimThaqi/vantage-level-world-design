using System;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Gameplay events the playtest logger (and anything else) can listen to without the senders knowing about it.
    /// </summary>
    public static class VantageEvents
    {
        public static event Action<string, Vector3> PlayerDied;             // cause, position
        public static event Action<string, Vector3> EnemyKilled;            // enemy name, position
        public static event Action<string> WeaponPickedUp;                  // weapon name
        public static event Action<int> LayoutGenerated;                    // enemy placement seed
        public static event Action LevelCompleted;

        public static void RaisePlayerDied(string cause, Vector3 position) => PlayerDied?.Invoke(cause, position);
        public static void RaiseEnemyKilled(string name, Vector3 position) => EnemyKilled?.Invoke(name, position);
        public static void RaiseWeaponPickedUp(string weapon) => WeaponPickedUp?.Invoke(weapon);
        public static void RaiseLayoutGenerated(int seed) => LayoutGenerated?.Invoke(seed);
        public static void RaiseLevelCompleted() => LevelCompleted?.Invoke();

        /// <summary>
        /// The player character, or null while it is dead or not spawned.
        /// </summary>
        public static CoverShooter.BaseActor ActivePlayer()
        {
            CoverShooter.BaseActor best = null;
            foreach (var actor in CoverShooter.Actors.All)
                if (actor != null && actor.Side != 0 && actor.IsAlive && actor.isActiveAndEnabled)
                    best = actor;
            return best;
        }
    }
}
