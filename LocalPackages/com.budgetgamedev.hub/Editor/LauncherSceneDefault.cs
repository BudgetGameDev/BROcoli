using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Hub.Editor
{
    /// <summary>
    /// Opens the launcher scene when the editor starts with no scene of its own.
    /// </summary>
    /// <remarks>
    /// Unity restores whatever was open when the editor last closed, and falls back
    /// to an empty untitled scene when it has nothing to restore: a fresh clone, or
    /// a wiped `Library/`. The launcher is where work on this project starts, so
    /// that empty scene is never what anyone wanted.
    ///
    /// Only that empty case is replaced. A restored scene was someone's deliberate
    /// choice and is left alone, and the check runs once per editor launch rather
    /// than on every domain reload, so recompiling a script never pulls the scene
    /// out from under whoever is working in it.
    /// </remarks>
    public static class LauncherSceneDefault
    {
        /// <summary>
        /// Survives domain reloads and resets when the editor restarts, which is
        /// exactly the "once per editor launch" this needs.
        /// </summary>
        private const string RanThisSessionKey = "BudgetGameDev.Hub.LauncherSceneDefault";

        [InitializeOnLoadMethod]
        private static void OpenOnEditorLaunch()
        {
            if (Application.isBatchMode || SessionState.GetBool(RanThisSessionKey, false))
                return;

            SessionState.SetBool(RanThisSessionKey, true);

            // Scenes cannot be opened mid-load; wait until the editor is idle.
            EditorApplication.delayCall += Open;
        }

        /// <summary>
        /// Whether what is open is Unity's placeholder rather than a real scene.
        /// </summary>
        /// <remarks>
        /// An unsaved scene still counts as authored work once it has been edited,
        /// and a second loaded scene means someone built a multi-scene setup, so
        /// neither is replaced.
        /// </remarks>
        /// <param name="loadedScenes">How many scenes are open in the hierarchy.</param>
        /// <param name="activeScenePath">Asset path of the active scene; empty when it was never saved.</param>
        /// <param name="isDirty">Whether the active scene has unsaved edits.</param>
        public static bool ShouldOpenLauncher(
            int loadedScenes,
            string activeScenePath,
            bool isDirty
        ) => loadedScenes == 1 && string.IsNullOrEmpty(activeScenePath) && !isDirty;

        private static void Open()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!ShouldOpenLauncher(SceneManager.sceneCount, active.path, active.isDirty))
                return;

            string launcher = HubBuildScenes.FindLauncherScene();
            if (launcher == null)
            {
                Debug.LogWarning(
                    $"[Hub] Launcher scene '{GameSession.LauncherSceneName}' is missing; "
                        + "leaving the empty scene open."
                );
                return;
            }

            EditorSceneManager.OpenScene(launcher, OpenSceneMode.Single);
        }
    }
}
