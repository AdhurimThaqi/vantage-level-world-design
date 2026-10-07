using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vantage.Testing;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Runs the automated playtest (VantageAutoplayBot) in Play mode: soldiers fight back, gates open, stairs climb.
    /// Batch: -executeMethod Vantage.EditorTools.VantageAutoplay.RunBatch   (exit code 0 = all passed)
    /// Editor: Vantage → Test → Run Autoplay Test (results in the Console, lines start with [Bot]).
    /// </summary>
    [InitializeOnLoad]
    public static class VantageAutoplay
    {
        private const string BatchKey = "Vantage.Autoplay.Batch";

        static VantageAutoplay()
        {
            EditorApplication.playModeStateChanged += onPlayMode;
        }

        [MenuItem("Vantage/Test/Run Autoplay Test", priority = 90)]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
            start(false);
        }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
            start(true);
        }

        private static void start(bool batch)
        {
            SessionState.SetBool(VantageAutoplayBot.RequestKey, true);
            SessionState.SetInt(VantageAutoplayBot.ResultKey, -1);
            SessionState.SetBool(BatchKey, batch);
            EditorApplication.EnterPlaymode();
        }

        private static void onPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(VantageAutoplayBot.RequestKey, false))
                return;
            SessionState.SetBool(VantageAutoplayBot.RequestKey, false);
            var result = SessionState.GetInt(VantageAutoplayBot.ResultKey, -1);
            Debug.Log($"[Bot] finished with code {result}");
            if (SessionState.GetBool(BatchKey, false))
                EditorApplication.Exit(result < 0 ? 3 : result);
        }
    }
}
