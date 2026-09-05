using System;
using BudgetGameDev.Autoplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Plays the player's own journey rather than a single life. It walks the run it
    /// was given somewhere worth saving, quits to the main menu, resumes the run from
    /// the save list, and checks that what came back is what left. Then it does the
    /// whole thing again with a second character and checks the two runs stayed apart,
    /// and finally dies on purpose to check that a death costs the run being played
    /// and leaves the other one alone.
    ///
    /// None of that happens by luck. An ordinary bot run enters the dungeon once and
    /// never leaves it, which leaves the save list, the continue path, and everything
    /// a resumed run rebuilds itself from as the least-tested code in the game despite
    /// being the code a player meets first. Dying is the same story from the other
    /// end: the bot is built to survive, so the screen every run really ends on is the
    /// one nothing ever reaches.
    ///
    /// A bot run must not cost a player their saves. The journey notes which of the
    /// ten slots already held a run before it started and frees every slot it claimed
    /// when the run ends, the same bargain <see cref="AutoplaySaveProbe"/> makes.
    /// </summary>
    public partial class AutoplaySaveJourneyDirector : MonoBehaviour
    {
        /// <summary>
        /// Wall-clock seconds one step may take. Steps are graded on real time rather
        /// than game time because what they watch for is the harness hanging -- a menu
        /// that never appears, a scene that never loads -- and under the fast-forward
        /// game-seconds say nothing about how long that took.
        /// </summary>
        private const float StepTimeout = 90f;

        /// <summary>
        /// Game-seconds of ordinary play before a run is worth resuming. A run
        /// checkpointed on its first frame would resume to the spawn point holding its
        /// starting stats, which is indistinguishable from a resume that restored
        /// nothing at all.
        /// </summary>
        private const float WalkSeconds = 12f;

        /// <summary>The order the journey goes in; each step is a thing a player does.</summary>
        internal enum Step
        {
            WalkFirstRun,
            LeaveFirstRun,
            ResumeFirstRun,
            VerifyFirstRun,
            ParkFirstRun,
            StartSecondRun,
            WalkSecondRun,
            LeaveSecondRun,
            ResumeSecondRun,
            VerifySecondRun,
            DieOnPurpose,
            Done,
        }

        /// <summary>
        /// Everything the journey presses and reads, gathered in one place so a test
        /// can stand in for the world rather than build a main menu and a dungeon to
        /// watch one state change. Each press reports whether it landed; one that has
        /// not landed yet is simply tried again next frame.
        /// </summary>
        internal sealed class Surroundings
        {
            internal Func<string> ActiveScene = () => SceneManager.GetActiveScene().name;
            internal Func<float> GameDelta = () => AutoplayTimeControl.GameDelta;
            internal Func<bool> QuitToMenu;
            internal Func<bool> StartAnotherRun;
            internal Func<int, bool> ResumeRun;
            internal Func<bool> Die;
            internal Action Checkpoint = BrocoliAutosaveController.SaveNow;
            internal AutoplaySaveProbe.CaptureRun CaptureLiveRun =
                BrocoliAutosaveController.TryCapture;
        }

        internal Surroundings World { get; } = new();

        internal Step Current { get; private set; } = Step.WalkFirstRun;

        /// <summary>Which slots were the player's before the run started.</summary>
        private bool[] occupiedAtStart;

        private BrocoliRunSave firstCheckpoint;
        private BrocoliRunSave secondCheckpoint;
        private int firstSlot = -1;
        private int secondSlot = -1;
        private float walked;
        private float stepDeadline;
        private bool verifiedSecondResume;
        private bool acted;
        private bool abandoned;

        private bool InDungeon => World.ActiveScene() == AutoplaySessionDirector.DungeonScene;

        private bool InMenu => World.ActiveScene() == AutoplaySessionDirector.MenuScene;

        private void Awake()
        {
            World.QuitToMenu ??= PressQuitToMenu;
            World.StartAnotherRun ??= PressNewRun;
            World.ResumeRun ??= PressPlayOnRun;
            World.Die ??= TakeAFatalHit;
        }

        /// <summary>
        /// Runs before the session director has pressed anything, which is the whole
        /// reason this component is created with the run rather than with the dungeon:
        /// the free slots have to be counted while they are still the player's, before
        /// the first run claims one of them.
        /// </summary>
        private void Start()
        {
            occupiedAtStart = new bool[BrocoliSaveSystem.MaxSaves];
            int free = 0;
            for (int slot = 0; slot < BrocoliSaveSystem.MaxSaves; slot++)
            {
                occupiedAtStart[slot] = BrocoliSaveSystem.TryLoad(slot, out _);
                if (!occupiedAtStart[slot])
                    free++;
            }

            if (free < 2)
            {
                Abandon(
                    $"the journey needs two free save slots and found {free}; "
                        + "delete a run from the menu, or run this tier on a fresh profile"
                );
                return;
            }

            RestartStepClock();
        }

        private void OnDestroy() => FreeClaimedSlots();

        private void OnApplicationQuit() => FreeClaimedSlots();

        private void Update()
        {
            // Only an autoplay run has any business pressing the menus on its own.
            if (!AutoplayController.IsActive || abandoned || Current == Step.Done)
                return;

            if (Time.realtimeSinceStartup > stepDeadline)
            {
                Abandon($"{Current} did not finish within {StepTimeout:0}s");
                return;
            }

            RunStep();
        }

        /// <summary>
        /// Each step presses once and then waits, so the switch reads in the order a
        /// player would do it. <see cref="acted"/> is what keeps a press from being
        /// repeated every frame while the scene it asked for is still loading.
        /// </summary>
        internal void RunStep()
        {
            switch (Current)
            {
                case Step.WalkFirstRun:
                    WalkAndCheckpoint(ref firstSlot, ref firstCheckpoint, Step.LeaveFirstRun);
                    break;
                case Step.LeaveFirstRun:
                    PressAndWait(World.QuitToMenu, () => InMenu, Step.ResumeFirstRun);
                    break;
                case Step.ResumeFirstRun:
                    PressAndWait(
                        () => World.ResumeRun(firstSlot),
                        () => InDungeon,
                        Step.VerifyFirstRun
                    );
                    break;
                case Step.VerifyFirstRun:
                    if (TryVerifyResume(firstCheckpoint))
                        Advance(Step.ParkFirstRun);
                    break;
                case Step.ParkFirstRun:
                    ParkFirstRun();
                    break;
                case Step.StartSecondRun:
                    StartSecondRun();
                    break;
                case Step.WalkSecondRun:
                    WalkAndCheckpoint(ref secondSlot, ref secondCheckpoint, Step.LeaveSecondRun);
                    break;
                case Step.LeaveSecondRun:
                    PressAndWait(World.QuitToMenu, () => InMenu, Step.ResumeSecondRun);
                    break;
                case Step.ResumeSecondRun:
                    PressAndWait(
                        () => World.ResumeRun(secondSlot),
                        () => InDungeon,
                        Step.VerifySecondRun
                    );
                    break;
                case Step.VerifySecondRun:
                    VerifySecondRun();
                    break;
                case Step.DieOnPurpose:
                    DieAndReadWhatItCost();
                    break;
            }
        }

        /// <summary>Presses until the press lands, then waits for what it asked for.</summary>
        private void PressAndWait(Func<bool> press, Func<bool> arrived, Step next)
        {
            if (!acted)
            {
                acted = press();
                return;
            }

            if (arrived())
                Advance(next);
        }

        private void Advance(Step next)
        {
            Current = next;
            acted = false;
            walked = 0f;
            RestartStepClock();
            if (next == Step.Done)
                Debug.Log("[Autoplay] The save journey went through every step it set out to.");
        }

        private void RestartStepClock() => stepDeadline = Time.realtimeSinceStartup + StepTimeout;

        /// <summary>
        /// Reports the journey as broken and stops driving. The error already fails
        /// the run, and letting the run carry on is what produces the report saying
        /// which of the steps before it had worked.
        /// </summary>
        private void Abandon(string reason)
        {
            abandoned = true;
            Current = Step.Done;
            Debug.LogError($"[Autoplay] Abandoning the save journey because {reason}.");
        }

        /// <summary>
        /// Hands back every slot the run claimed. Slots that already held a run when
        /// the journey started are left exactly as they were found, so a bot run on a
        /// real profile costs the player nothing.
        ///
        /// Freeing the run's own slot clears the active-slot pointer, so whatever that
        /// held is put back: the session director restores the player's own value on
        /// its way out, and this must not undo that whichever order the two are torn
        /// down in.
        /// </summary>
        internal void FreeClaimedSlots()
        {
            if (occupiedAtStart == null)
                return;

            int pointer = PlayerPrefs.GetInt(BrocoliSaveSystem.ActiveSlotKey, -1);
            for (int slot = 0; slot < BrocoliSaveSystem.MaxSaves; slot++)
                if (!occupiedAtStart[slot])
                    BrocoliSaveSystem.DeleteSave(slot);

            PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, pointer);
            PlayerPrefs.Save();
            occupiedAtStart = null;
        }
    }
}
