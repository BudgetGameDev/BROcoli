using UnityEngine;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// When the pointer is on screen, as a decision with no Unity state in it.
    ///
    /// There are two reasons to show a pointer and they behave differently. A screen built to
    /// be clicked -- inventory, map, pause, main menu -- holds it up for as long as it is open,
    /// with no timer involved. Everywhere else the pointer is in the way of a game played on
    /// the keyboard, so it is revealed by the act of moving the mouse and then withdrawn again,
    /// which is what keeps it from sitting over the dungeon for the rest of the run.
    /// </summary>
    public static class PointerRevealPolicy
    {
        /// <summary>How long moving the mouse keeps the pointer on screen during play.</summary>
        public const float RevealSeconds = 5f;

        /// <summary>
        /// How far the mouse has to travel, in pixels, before it counts as moved. Mice report
        /// sub-pixel jitter while sitting still, and a player who is not touching the mouse
        /// should not have the pointer kept alive by it.
        /// </summary>
        public const float MovementThresholdPixels = 2f;

        /// <summary>
        /// Whether the pointer is shown at all. <paramref name="secondsSinceMoved"/> is
        /// unscaled: the reveal has to expire at the same rate while the game is paused or
        /// slowed, because it is about the player's hand, not about the dungeon's clock.
        /// </summary>
        public static bool ShouldShowPointer(bool heldVisible, float secondsSinceMoved) =>
            heldVisible || secondsSinceMoved < RevealSeconds;
    }
}
