using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class AutoplaySaveJourneyDirector
    {
        /// <summary>
        /// How far from where they stopped a resumed player may be. The check runs a
        /// frame or two into the resumed run, by which time they have settled onto the
        /// floor; a resume that lost the position puts them back at the spawn point,
        /// which is rooms away rather than centimetres.
        /// </summary>
        private const float ResumeTolerance = 1f;

        /// <summary>
        /// Health a resumed run may differ by. A tick of regeneration between the
        /// restore and the check is not a lost save; coming back on full health, or on
        /// none, is.
        /// </summary>
        private const float HealthTolerance = 1f;

        /// <summary>
        /// Plays for a while and then checkpoints, which is what a player does without
        /// thinking about it. The slot is read here rather than when the run started
        /// because it is the menu that claims one.
        /// </summary>
        private void WalkAndCheckpoint(ref int slot, ref BrocoliRunSave checkpoint, Step next)
        {
            if (!InDungeon)
                return;

            if (slot < 0)
                slot = BrocoliSaveSystem.ActiveSlot;

            walked += World.GameDelta();
            if (walked < WalkSeconds || slot < 0)
                return;

            World.Checkpoint();
            if (!BrocoliSaveSystem.TryLoad(slot, out BrocoliRunSave written))
                return;

            checkpoint = written;
            Advance(next);
        }

        /// <summary>
        /// Whether the run that came back has been confirmed to be the run that left.
        /// False covers both "the restore has not finished" and "it finished wrong":
        /// the second abandons the journey on its way through, so the caller has
        /// nothing to tell apart.
        /// </summary>
        private bool TryVerifyResume(BrocoliRunSave checkpoint)
        {
            if (!InDungeon || checkpoint == null)
                return false;

            // Consuming the pending checkpoint is the last thing a resume does, so
            // this is the first frame on which everything has been put back.
            if (BrocoliSaveSystem.TryGetPendingContinue(out _))
                return false;

            if (!World.CaptureLiveRun(out BrocoliRunSave live))
                return false;

            if (!ResumedFrom(checkpoint, live, out string difference))
            {
                Abandon($"the resumed run came back with a different {difference}");
                return false;
            }

            AutoplayFeatureLog.Record(AutoplayFeatures.SaveResumed);
            return true;
        }

        /// <summary>
        /// Leaves the first character parked in the menu and re-reads the run it was
        /// parked with. That is the state nothing else in the journey may touch, and
        /// the state the second character must not overwrite.
        /// </summary>
        private void ParkFirstRun()
        {
            if (!acted)
            {
                acted = World.QuitToMenu();
                return;
            }

            if (!InMenu || !BrocoliSaveSystem.TryLoad(firstSlot, out BrocoliRunSave parked))
                return;

            firstCheckpoint = parked;
            Advance(Step.StartSecondRun);
        }

        /// <summary>
        /// Starts a second character from the saves panel. A second run that landed in
        /// the first one's slot is the defect this whole journey exists to catch, so it
        /// is reported here rather than left to be noticed as a strange resume later.
        /// </summary>
        private void StartSecondRun()
        {
            if (!acted)
            {
                acted = World.StartAnotherRun();
                return;
            }

            if (!InDungeon)
                return;

            secondSlot = BrocoliSaveSystem.ActiveSlot;
            if (secondSlot < 0)
            {
                Abandon("the second run claimed no save slot");
                return;
            }

            if (secondSlot == firstSlot)
            {
                Abandon($"the second run claimed slot {secondSlot}, which the first run holds");
                return;
            }

            Advance(Step.WalkSecondRun);
        }

        /// <summary>
        /// Checks the second character resumed, and then that the first one is still
        /// sitting in its own slot exactly as it was parked. Two runs that each resume
        /// correctly but share one slot would pass every check before this one.
        /// </summary>
        private void VerifySecondRun()
        {
            if (!verifiedSecondResume)
            {
                verifiedSecondResume = TryVerifyResume(secondCheckpoint);
                return;
            }

            if (!BrocoliSaveSystem.TryLoad(firstSlot, out BrocoliRunSave kept))
            {
                Abandon($"starting and playing a second run emptied slot {firstSlot}");
                return;
            }

            if (!ResumedFrom(firstCheckpoint, kept, out string difference))
            {
                Abandon(
                    $"the run parked in slot {firstSlot} came back with a different {difference}"
                );
                return;
            }

            AutoplayFeatureLog.Record(AutoplayFeatures.SaveSlotsIndependent);
            Advance(Step.DieOnPurpose);
        }

        /// <summary>
        /// Dies on purpose and reads what the death cost. Dying drops the run being
        /// played -- that is what a roguelite death means here -- and it must drop only
        /// that one: the other character is parked in the menu and has nothing to do
        /// with it.
        /// </summary>
        private void DieAndReadWhatItCost()
        {
            if (!acted)
            {
                acted = World.Die();
                return;
            }

            if (BrocoliSaveSystem.TryLoad(secondSlot, out _))
                return; // the death has not reached the save yet

            if (!BrocoliSaveSystem.TryLoad(firstSlot, out BrocoliRunSave survivor))
            {
                Abandon($"dying in slot {secondSlot} also took the run in slot {firstSlot}");
                return;
            }

            if (!ResumedFrom(firstCheckpoint, survivor, out string difference))
            {
                Abandon($"the run that survived the death has a different {difference}");
                return;
            }

            AutoplayFeatureLog.Record(AutoplayFeatures.SaveSurvivedAnotherRunsDeath);
            Advance(Step.Done);
        }

        /// <summary>
        /// Pauses and presses Main Menu, which is the way a player leaves a run and
        /// the way that checkpoints on its way out.
        /// </summary>
        private bool PressQuitToMenu()
        {
            PauseMenu menu = FindAnyObjectByType<PauseMenu>();
            if (menu == null || menu.mainMenuButton == null)
                return false;

            if (!menu.IsPaused())
            {
                menu.TogglePause();
                AutoplayFeatureLog.RecordIf(menu.IsPaused(), AutoplayFeatures.PauseToggled);
            }

            menu.mainMenuButton.onClick.Invoke();
            return true;
        }

        private bool PressNewRun()
        {
            ResponsiveMainMenuLayout layout = FindAnyObjectByType<ResponsiveMainMenuLayout>();
            return layout != null && layout.PressNewRun();
        }

        private bool PressPlayOnRun(int slot)
        {
            ResponsiveMainMenuLayout layout = FindAnyObjectByType<ResponsiveMainMenuLayout>();
            return layout != null && slot >= 0 && layout.PressPlayOnRun(slot);
        }

        /// <summary>
        /// Takes a hit big enough to finish the run, through the entry point an
        /// enemy's strike lands on. The damage handler holds an immunity window open
        /// after every hit, so a strike it refused is simply thrown again next frame
        /// rather than read as a game that will not let the player die.
        /// </summary>
        private static bool TakeAFatalHit()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
                return false;

            PlayerDamageHandler damage = player.GetComponent<PlayerDamageHandler>();
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (damage == null || stats == null)
                return false;

            // Armour comes off a hit before it lands, so the blow is sized to get
            // through it rather than to be exactly lethal.
            if (!damage.IsGameOver)
                damage.TakeMeleeDamage(FatalHitAmount(stats.CurrentHealth, stats.CurrentArmor));

            return damage.IsGameOver;
        }

        internal static float FatalHitAmount(float health, float armor) =>
            Mathf.Max(1f, health + armor + 1f);

        /// <summary>
        /// Whether a run came back as the run that was written. Everything a resumed
        /// run is rebuilt from -- its level, its experience, its dungeon, and how much
        /// of that dungeon has been seen -- has to be exactly what was stored; position
        /// and health are judged with a little slack because the check runs a frame or
        /// two into the resumed run rather than inside the restore.
        /// </summary>
        internal static bool ResumedFrom(
            BrocoliRunSave written,
            BrocoliRunSave read,
            out string difference
        )
        {
            difference = "run";
            if (
                written?.player == null
                || written.game == null
                || written.dungeon == null
                || read?.player == null
                || read.game == null
                || read.dungeon == null
            )
            {
                return false;
            }

            if (!Mathf.Approximately(written.player.level, read.player.level))
                difference = $"level ({written.player.level} became {read.player.level})";
            else if (!Mathf.Approximately(written.player.experience, read.player.experience))
                difference = "experience";
            else if (written.dungeon.seed != read.dungeon.seed)
                difference = $"dungeon ({written.dungeon.seed} became {read.dungeon.seed})";
            else if (written.dungeon.roomsVisited != read.dungeon.roomsVisited)
                difference = "count of rooms visited";
            else if (written.game.enemiesKilled != read.game.enemiesKilled)
                difference = "count of enemies killed";
            else if (Mathf.Abs(written.player.health - read.player.health) > HealthTolerance)
                difference = $"health ({written.player.health} became {read.player.health})";
            else if (
                Vector2.Distance(written.playerPosition.ToGround(), read.playerPosition.ToGround())
                > ResumeTolerance
            )
                difference = "position";
            else
                return true;

            return false;
        }
    }
}
