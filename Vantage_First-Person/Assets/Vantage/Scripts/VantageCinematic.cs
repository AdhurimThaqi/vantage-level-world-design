using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Every camera takeover (the intro, the extraction) goes through here. Begin switches off the player's input
    /// and damage and the camera's follow and wall fade; End puts back exactly what Begin switched off, so a part
    /// that was already off (the autoplay bot drives the player without input) stays off.
    /// </summary>
    public static class VantageCinematic
    {
        public static bool Active { get; private set; }

        private static ThirdPersonInput _input;
        private static CharacterHealth _health;
        private static Behaviour _follow, _fader;

        public static void Begin()
        {
            if (Active) return;
            Active = true;
            var player = VantageEvents.ActivePlayer();
            if (player != null)
            {
                _input = switchOff(player.GetComponent<ThirdPersonInput>());
                var health = player.GetComponent<CharacterHealth>();
                if (health != null && health.IsTakingDamage) { health.IsTakingDamage = false; _health = health; }
            }
            var cam = Camera.main;
            if (cam != null)
            {
                _follow = switchOff(cam.GetComponent<ThirdPersonCamera>());
                _fader = switchOff(cam.GetComponent<VantageCameraFader>());
            }
            VantageEvents.RaiseCinematicStarted();
        }

        public static void End()
        {
            if (!Active) return;
            Active = false;
            if (_input != null) _input.enabled = true;
            if (_health != null) _health.IsTakingDamage = true;
            if (_follow != null) _follow.enabled = true;
            if (_fader != null) _fader.enabled = true;
            forget();
            VantageEvents.RaiseCinematicEnded();
        }

        private static T switchOff<T>(T part) where T : Behaviour
        {
            if (part == null || !part.enabled) return null;
            part.enabled = false;
            return part;
        }

        private static void forget()
        {
            _input = null;
            _health = null;
            _follow = null;
            _fader = null;
        }

        // Play mode can be entered without a domain reload: start every play session without a takeover. A scene
        // reload (death, play again after the extraction) brings a fresh player and camera, so it ends any takeover.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void clearOnPlay()
        {
            Active = false;
            forget();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= onSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += onSceneLoaded;
        }

        private static void onSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Active = false;
            forget();
        }
    }
}
