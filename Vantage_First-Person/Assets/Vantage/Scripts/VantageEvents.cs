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

        public static event Action<string> Message;                         // short notice shown on the HUD
        public static event Action<string> AlarmRaised;                     // source: "trigger" or "camera"
        public static event Action AlarmStopped;
        public static event Action<string> ItemPickedUp;                    // tower inventory item (Key, Gun)
        public static event Action<int, int> CoinCollected;                 // collected, total
        public static event Action BossSpawned;
        public static event Action GameWon;

        public static void RaiseMessage(string text) => Message?.Invoke(text);
        public static void RaiseAlarm(string source) => AlarmRaised?.Invoke(source);
        public static void RaiseAlarmStopped() => AlarmStopped?.Invoke();
        public static void RaiseItemPickedUp(string item) => ItemPickedUp?.Invoke(item);
        public static void RaiseCoinCollected(int collected, int total) => CoinCollected?.Invoke(collected, total);
        public static void RaiseBossSpawned() => BossSpawned?.Invoke();
        public static void RaiseGameWon() => GameWon?.Invoke();

        /// <summary>
        /// Interaction prompt the HUD shows as "[E] Prompt" (null = none). Set by whatever the player is in range of;
        /// cleared by the same object when the player leaves (only if it still owns it).
        /// </summary>
        public static string Prompt { get; private set; }
        private static object _promptOwner;

        public static void SetPrompt(object owner, string text) { _promptOwner = owner; Prompt = text; }
        public static void ClearPrompt(object owner) { if (_promptOwner == owner) { _promptOwner = null; Prompt = null; } }

        /// <summary>Static events outlive a scene reload; anything static that subscribes clears itself here.</summary>
        public static void ResetPrompt() { _promptOwner = null; Prompt = null; }

        private static CoverShooter.BaseActor _player;
        private static int _playerFrame = -1;

        /// <summary>
        /// The player character, or null while it is dead or not spawned. Looked up once per frame
        /// (drones, soldiers, camera and minimap all ask every frame).
        /// </summary>
        public static CoverShooter.BaseActor ActivePlayer()
        {
            if (_playerFrame == Time.frameCount)
                return _player;
            _playerFrame = Time.frameCount;
            _player = null;
            for (int i = 0; i < CoverShooter.Actors.Count; i++)
            {
                var actor = CoverShooter.Actors.Get(i);
                if (actor != null && actor.Side != 0 && actor.IsAlive && actor.isActiveAndEnabled)
                    _player = actor;
            }
            return _player;
        }
    }
}
