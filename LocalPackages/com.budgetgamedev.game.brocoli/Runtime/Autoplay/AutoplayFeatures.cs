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
        /// Systems that depend on the run's luck -- an elite spawning, a hydra
        /// surviving long enough to split, the player actually dying. They are
        /// reported so a run can be read, but never fail it.
        /// </summary>
        internal static readonly string[] Optional =
        {
            MainMenuContinue,
            EliteKilled,
            HydraSplit,
            ProjectileDodged,
            InventoryEquipped,
            GameOverShown,
            GameOverRestart,
        };

        /// <summary>
        /// Moments recorded so a run can be watched rather than graded. They gate
        /// nothing -- what they buy is a name a <c>--capture-on</c> trigger can point
        /// at, and a count in the ledger.
        /// </summary>
        internal static readonly string[] Observed = { ExperienceDropped };
    }
}
