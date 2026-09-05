using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// The seams the landscape enforcer reaches the engine through, and the reset
    /// that puts every static it owns back to its first-run value.
    /// </summary>
    public static partial class ForceLandscapeAspect
    {
        /// <summary>
        /// How a helper object is kept alive across scene loads.
        /// </summary>
        /// <remarks>
        /// <see cref="UnityEngine.Object.DontDestroyOnLoad"/> is a play-mode-only call: outside
        /// play mode it throws rather than doing nothing, which would take the
        /// whole enforcer down if anything ever drove it from an editor context.
        /// Routing it through a field lets that context substitute its own answer.
        /// </remarks>
        internal static Action<GameObject> KeepAcrossScenes = UnityEngine.Object.DontDestroyOnLoad;

        /// <summary>
        /// How the loaded scene's pause screen is found. Menus deliberately have
        /// none, and the enforcer must behave when there is none, so the lookup is
        /// substitutable rather than hard-wired to a scene sweep.
        /// </summary>
        internal static Func<IPauseController> FindPauseController = PauseControllerLocator.Find;

        /// <summary>
        /// Whether the running player is the editor's own. Play mode hands focus to
        /// the console, the inspector and the scene view constantly, so auto-pausing
        /// on focus loss would stop the game every time the developer clicks off it.
        /// A built player keeps the pause. Substitutable because an editor test is
        /// always running in the editor and still has to drive both answers.
        /// </summary>
        internal static Func<bool> IsEditorPlayer = () => Application.isEditor;

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
        /// <summary>Lets development automation keep running when its window loses focus.</summary>
        public static bool SuppressFocusLossPause { get; set; }
#endif

        /// <summary>
        /// Clears every static this type owns. Statics survive a play session when
        /// domain reloading is off, so the next run - and each test - has to start
        /// from the same state the very first run sees.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            KeepAcrossScenes = UnityEngine.Object.DontDestroyOnLoad;
            FindPauseController = PauseControllerLocator.Find;
            IsEditorPlayer = () => Application.isEditor;
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
            SuppressFocusLossPause = false;
#endif
            _initialized = false;
            _isPortrait = false;
            _isFocusLost = false;
            _savedTimeScale = 1f;
            _rotateOverlay = null;
            _lastScreenWidth = 0;
            _lastScreenHeight = 0;
            _lastOrientationChangeTime = -999f;
            _lastScreenChangeCheck = 0f;
            ENFORCE_MAX_ASPECT = false;
            DEBUG_MODE = false;
            RotateAnimator.ResetStatics();
        }
    }
}
