using System;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// The catalogue of player-facing systems an autoplay run is expected to reach.
    /// Splitting them into required and optional is what makes the <c>coverage</c>
    /// scenario meaningful: a required feature that never fired is a harness that
    /// stopped testing something, not a run that got unlucky.
    /// </summary>
    internal static class AutoplayFeatures
    {
        internal const string MainMenuShown = "menu.shown";
        internal const string MainMenuNewGame = "menu.new-game";
        internal const string MainMenuContinue = "menu.continue";
        internal const string RoomEntered = "dungeon.room-entered";
        internal const string DoorTraversed = "dungeon.door-traversed";
        internal const string ChestOpened = "dungeon.chest-opened";
        internal const string EnemyKilled = "combat.enemy-killed";
        internal const string EliteKilled = "combat.elite-killed";
        internal const string HydraSplit = "combat.hydra-split";
        internal const string ProjectileDodged = "combat.projectile-dodged";
        internal const string DamageTaken = "combat.damage-taken";
        internal const string ExperienceDropped = "pickup.experience-dropped";
        internal const string ExperienceCollected = "pickup.experience";
        internal const string BoostCollected = "pickup.boost";
        internal const string UpgradeChosen = "levelup.upgrade-chosen";
        internal const string PauseToggled = "ui.pause-toggled";
        internal const string PauseSettings = "ui.pause-settings";
        internal const string InventoryOpened = "ui.inventory-opened";
        internal const string InventoryNavigated = "ui.inventory-navigated";
        internal const string InventoryEquipped = "ui.inventory-equipped";
        internal const string MapOpened = "ui.map-opened";
        internal const string MapPanned = "ui.map-panned";
        internal const string SaveRoundTrip = "save.round-trip";
        internal const string SaveCheckpointed = "save.checkpointed";
        internal const string SaveResumed = "save.resumed";
        internal const string SaveSlotsIndependent = "save.slots-independent";
        internal const string SaveDropped = "save.dropped";
        internal const string SaveSurvivedAnotherRunsDeath = "save.survived-another-runs-death";
        internal const string GameOverShown = "gameover.shown";
        internal const string GameOverRestart = "gameover.restart";

        /// <summary>
        /// Systems every full run must reach. Each one is either unconditional or
        /// something the bot deliberately steers toward, so a miss is a real defect.
        /// </summary>
        internal static readonly string[] Required =
        {
            MainMenuShown,
            MainMenuNewGame,
            RoomEntered,
            DoorTraversed,
            ChestOpened,
            EnemyKilled,
            DamageTaken,
            ExperienceCollected,
            BoostCollected,
            UpgradeChosen,
            PauseToggled,
            PauseSettings,
            InventoryOpened,
            InventoryNavigated,
            MapOpened,
            MapPanned,
            SaveRoundTrip,
        };

        /// <summary>
        /// Systems no ordinary run is failed for. Some depend on its luck -- an elite
        /// spawning, a hydra surviving long enough to split -- and the rest belong to
        /// a journey a combat run never takes: leaving to the menu, resuming, dying.
        /// They are reported so a run can be read, and required where they are
        /// deliberately driven.
        /// </summary>
        internal static readonly string[] Optional =
        {
            MainMenuContinue,
            EliteKilled,
            HydraSplit,
            ProjectileDodged,
            InventoryEquipped,
            SaveCheckpointed,
            SaveResumed,
            SaveSlotsIndependent,
            SaveDropped,
            SaveSurvivedAnotherRunsDeath,
            GameOverShown,
            GameOverRestart,
        };

        /// <summary>
        /// Moments recorded so a run can be watched rather than graded. They gate
        /// nothing -- what they buy is a name a <c>--capture-on</c> trigger can point
        /// at, and a count in the ledger.
        /// </summary>
        internal static readonly string[] Observed = { ExperienceDropped };

        /// <summary>
        /// The player's own journey, which the <c>journey</c> scenario drives from
        /// end to end: two runs made from the menu, walked somewhere, quit to the
        /// menu and resumed, and then a death that has to cost the run being played
        /// and only that one. Every entry here is something the journey deliberately
        /// does, so a miss is the harness reporting that the journey broke rather
        /// than that the run was unlucky.
        /// </summary>
        internal static readonly string[] SaveJourney =
        {
            MainMenuShown,
            MainMenuNewGame,
            RoomEntered,
            SaveCheckpointed,
            MainMenuContinue,
            SaveResumed,
            SaveSlotsIndependent,
            GameOverShown,
            SaveDropped,
            SaveSurvivedAnotherRunsDeath,
            GameOverRestart,
        };

        /// <summary>
        /// What a scenario has to have reached to pass. Only the two sweeps grade
        /// themselves on the ledger; the rest are graded on surviving, levelling, or
        /// staying in band, and are handed the coverage list purely so their reports
        /// read the same way.
        /// </summary>
        internal static string[] RequiredFor(string scenario) =>
            string.Equals(scenario, JourneyScenario, StringComparison.Ordinal)
                ? SaveJourney
                : Required;

        /// <summary>The scenario name graded on the journey rather than on coverage.</summary>
        internal const string JourneyScenario = "journey";
    }
}
