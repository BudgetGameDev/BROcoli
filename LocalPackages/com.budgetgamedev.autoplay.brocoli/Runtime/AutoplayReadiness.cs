using BudgetGameDev.Autoplay;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>Only accelerated runs wait for streamed geometry and its asynchronous NavMesh job.</summary>
    [DefaultExecutionOrder(-32000)]
    internal sealed class AutoplayReadiness : MonoBehaviour
    {
        internal readonly SimulationReadinessGate Gate = new();
        private DungeonManager dungeon;
        private bool ownsPause;
        private float previousScale;

        // Early Update excludes a pending job from telemetry; LateUpdate catches a
        // room transition that started streaming later in this frame.
        private void Update() => Observe();

        private void LateUpdate() => Observe();

        private void Observe()
        {
            if (dungeon == null)
                dungeon = FindAnyObjectByType<DungeonManager>();
            bool ready = dungeon == null || !dungeon.NavigationUpdatePending;
            Gate.Observe(ready, Time.realtimeSinceStartupAsDouble);
            AutoplayTimeControl.WaitingForReadiness = Gate.Waiting;
            if (Gate.Waiting)
            {
                if (Time.timeScale > 0f)
                {
                    previousScale = Time.timeScale;
                    ownsPause = true;
                    Time.timeScale = 0f;
                }
            }
            else
                ReleasePause();
        }

        internal static bool GameplayPaused =>
            PauseMenu.AnyPaused
            || LevelUpScreen.AnyShowing
            || (GameOverOverlay.Active != null && GameOverOverlay.Active.IsVisible);

        private void ReleasePause()
        {
            if (ShouldRestoreScale(ownsPause, Time.timeScale, GameplayPaused))
                Time.timeScale = previousScale;
            ownsPause = false;
        }

        internal static bool ShouldRestoreScale(
            bool owned,
            float currentScale,
            bool gameplayPaused
        ) => owned && currentScale == 0f && !gameplayPaused;

        private void OnDisable()
        {
            ReleasePause();
            AutoplayTimeControl.WaitingForReadiness = false;
        }
    }
}
