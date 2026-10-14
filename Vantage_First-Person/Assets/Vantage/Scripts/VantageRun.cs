using System.Collections.Generic;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// One play-through, kept across the scene reload that follows a death: the level to restart at (checkpoint),
    /// the weapons and coins already collected, deaths and the start time (for the end screen).
    /// </summary>
    public static class VantageRun
    {
        public static bool Started { get; private set; }
        /// <summary>Index of the level the player restarts at after dying (VantageTowerLevels.Levels).</summary>
        public static int Checkpoint;
        public static readonly HashSet<string> Weapons = new HashSet<string>();
        public static readonly HashSet<string> Coins = new HashSet<string>();
        public static int Deaths;
        public static float StartTime;

        public static float Elapsed => Time.realtimeSinceStartup - StartTime;

        /// <summary>Starts a run on the first load; does nothing on the reloads after a death.</summary>
        public static void Begin()
        {
            if (Started) return;
            Started = true;
            Checkpoint = 0;
            Weapons.Clear();
            Coins.Clear();
            Deaths = 0;
            StartTime = Time.realtimeSinceStartup;
        }

        /// <summary>Forgets the run (new game from the end screen, or the autoplay bot).</summary>
        public static void Reset()
        {
            Started = false;
            Begin();
        }

        /// <summary>
        /// Reloads the level scene: after a death (the checkpoint takes it from there) and for a new run. In the
        /// editor through EditorSceneManager (works whether or not the scene is in Build Settings).
        /// </summary>
        public static void ReloadLevel()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        // Play mode can be entered without a domain reload: start every play session fresh.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void clearOnPlay()
        {
            Started = false;
            VantageEvents.ResetPrompt();
        }
    }
}
