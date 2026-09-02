using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Entry point for automated playtest ("autoplay") runs. Activated only when the
    /// build or editor is launched with <c>--autoplay</c> (or env
    /// <c>BROCOLI_AUTOPLAY=1</c>). When not requested this class does nothing, so
    /// normal play is unaffected.
    ///
    /// A run is configured entirely by <see cref="AutoplayConfig"/>, which understands
    /// named tiers, so the same player binary runs every tier on macOS, Linux, and
    /// Windows without a launcher script in between.
    /// </summary>
    public class AutoplayController : MonoBehaviour
    {
        public static bool IsActive { get; private set; }

        /// <summary>
        /// Whether the run may checkpoint itself into a real save slot. Off for an
        /// ordinary bot run, which must not claim one of the player's ten; on for
        /// the save journey, whose whole subject is checkpointing and which hands
        /// back every slot it claimed when the run ends.
        /// </summary>
        internal static bool CheckpointsEnabled { get; private set; }

        private AutoplayConfig _config;
        private bool _wired;
        private float _volumeBeforeRun = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Bootstrap(AutoplayConfig.FromCommandLine());
        }

        internal static AutoplayController Bootstrap(AutoplayConfig config) =>
            config.Enabled ? StartAutoplay(config) : null;

        internal static AutoplayController StartAutoplay(AutoplayConfig config)
        {
            IsActive = true;
            CheckpointsEnabled = config.ExerciseSaveJourney;
            AutoplayFeatureLog.Reset();
            AutoplayScalingLog.Reset();

            var go = new GameObject("[AutoplayController]");
            DontDestroyOnLoad(go);
            var controller = go.AddComponent<AutoplayController>();
            controller._config = config;
            controller.Begin();
            return controller;
        }

        private void Begin()
        {
            Debug.Log($"[Autoplay] Starting. {_config}");

            Application.runInBackground = true;
            UnityEngine.Random.InitState(_config.Seed);
            AutoplayCaptureTriggers.Arm(_config.CaptureOn);

            // Make each frame cheap so the fake-time fast-forward actually accelerates even
            // in heavy combat. Capturing stack traces for log/warning spam (thousands of
            // pool-capacity warnings) and high-res/quality rendering otherwise pin us near
            // real-time. Exceptions/errors keep their traces for debugging.
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            QualitySettings.SetQualityLevel(0, true);

            // A run is watched through the pictures it leaves behind, so it plays at the
            // display's own size rather than in a thumbnail window: a full-screen frame is
            // legible as a screenshot and shows the HUD at the size a player reads it.
            Display display = Display.main;
            Screen.SetResolution(
                display.systemWidth,
                display.systemHeight,
                FullScreenMode.FullScreenWindow
            );

            // Nobody sits and listens to a bot play, and a run often plays in the
            // background while its watcher is doing something else. The listener's volume
            // is not a saved preference, so silencing the run leaves the player's own
            // audio settings untouched, and OnDestroy hands the editor its sound back.
            _volumeBeforeRun = AudioListener.volume;
            AudioListener.volume = 0f;

            if (_config.Deterministic)
                BeginFastForward();

            // Capture spans the whole session rather than the dungeon alone: a trigger
            // may name something that happens in a menu, and a run that never renders
            // its first screen is exactly what the picture check is for.
            var capture = gameObject.AddComponent<FrameCapture>();
            capture.Configure(_config);

            SceneManager.sceneLoaded += OnSceneLoaded;
            EnterGame(SceneManager.LoadScene);
        }

        /// <summary>
        /// Starts the session at the main menu when the tier asks for the full
        /// player journey, and at the dungeon when it only wants gameplay.
        /// </summary>
        internal void EnterGame(Action<string> loadScene)
        {
            if (_config.DriveMenus)
                gameObject.AddComponent<AutoplaySessionDirector>();

            // Created with the run rather than with the dungeon, and ahead of the
            // telemetry: the journey has to count the free save slots while they are
            // still the player's, and it reads what a deliberate death cost on the
            // frame it happens rather than after the telemetry has restarted the run.
            if (_config.ExerciseSaveJourney)
                gameObject.AddComponent<AutoplaySaveJourneyDirector>();

            loadScene(
                _config.DriveMenus
                    ? AutoplaySessionDirector.MenuScene
                    : AutoplaySessionDirector.DungeonScene
            );
        }

        private void BeginFastForward()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            float step = AutoplayTimeControl.ResolveCaptureStep(
                _config.Timestep,
                Time.fixedDeltaTime
            );
            Time.captureDeltaTime = step;
            Debug.Log(
                $"[Autoplay] Fast-forward at {step:0.####}s of game time per frame "
                    + $"(physics step {Time.fixedDeltaTime:0.####}s)."
            );
        }

        private void OnDestroy()
        {
            AudioListener.volume = _volumeBeforeRun;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != AutoplaySessionDirector.DungeonScene || _wired)
                return;

            _wired = true;

            // Re-assert the seed right before gameplay objects spin up.
            UnityEngine.Random.InitState(_config.Seed);

            gameObject.AddComponent<BotDriver>();
            gameObject.AddComponent<LevelUpAutoResolver>();
            if (_config.ExerciseFeatures)
                gameObject.AddComponent<AutoplayFeatureDirector>();

            var telemetry = gameObject.AddComponent<RunTelemetry>();
            telemetry.Configure(_config);

            Debug.Log(
                "[Autoplay] Dungeon scene wired (autonomous navigation + combat policy + "
                    + "telemetry + adaptive upgrades)."
            );
        }
    }
}
