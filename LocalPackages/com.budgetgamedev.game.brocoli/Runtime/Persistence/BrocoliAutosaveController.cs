using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>Captures the live run frequently and at application lifecycle boundaries.</summary>
    [DefaultExecutionOrder(1000)]
    internal sealed class BrocoliAutosaveController : MonoBehaviour
    {
        private const string DungeonScene = "Brocoli_Dungeon";
        private const float SaveIntervalSeconds = 5f;

        private static BrocoliAutosaveController active;
        private bool ready;
        private float nextSaveTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePresentInDungeon()
        {
            if (SceneManager.GetActiveScene().name == DungeonScene)
                EnsurePresent();
        }

        internal static void EnsurePresent()
        {
            // A bot run is a throwaway: checkpointing it would claim one of the ten
            // save slots and leave the player a run they never played.
            if (active != null || AutoplayController.IsActive)
                return;

            new GameObject("[Brocoli Autosave]").AddComponent<BrocoliAutosaveController>();
        }

        private void Awake()
        {
            if (active != null && active != this)
            {
                Destroy(gameObject);
                return;
            }

            active = this;
        }

        private IEnumerator Start()
        {
            // This component runs after ordinary gameplay Starts, including ResetStats.
            if (BrocoliSaveSystem.TryGetPendingContinue(out BrocoliRunSave save))
                PlayerStats.Resolve()?.RestoreRunState(save.player);

            yield return null;
            BrocoliSaveSystem.FinishContinue();
            ready = true;
            SaveCheckpoint();
            nextSaveTime = Time.realtimeSinceStartup + SaveIntervalSeconds;
        }

        private void Update()
        {
            if (!ready || Time.realtimeSinceStartup < nextSaveTime)
                return;

            SaveCheckpoint();
            nextSaveTime = Time.realtimeSinceStartup + SaveIntervalSeconds;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveCheckpoint();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                SaveCheckpoint();
        }

        private void OnApplicationQuit() => SaveCheckpoint();

        private void OnDestroy()
        {
            if (active != this)
                return;

            SaveCheckpoint();
            active = null;
        }

        internal static void SaveNow()
        {
            active?.SaveCheckpoint();
        }

        private void SaveCheckpoint()
        {
            if (!ready || !TryCapture(out BrocoliRunSave save))
                return;

            BrocoliSaveSystem.Save(save);
        }

        private static bool TryCapture(out BrocoliRunSave save)
        {
            save = null;
            PlayerStats stats = PlayerStats.Resolve();
            GameStates game = FindAnyObjectByType<GameStates>();
            DungeonManager dungeon = FindAnyObjectByType<DungeonManager>();
            Transform player = stats != null ? stats.transform.root : null;

            if (
                stats == null
                || !stats.IsAlive
                || game == null
                || game.IsGameOver
                || dungeon == null
                || dungeon.Seed == 0
                || player == null
            )
            {
                return false;
            }

            save = new BrocoliRunSave
            {
                mobileControls = PlayerPrefs.GetInt("ShowVirtualController", 0) == 1,
                playerPosition = player.position,
                player = stats.CaptureRunState(),
                game = game.CaptureRunState(),
                dungeon = dungeon.CaptureRunState(),
            };
            return true;
        }
    }
}
