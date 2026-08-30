namespace BudgetGameDev.Shared
{
    /// <summary>
    /// A game's pause screen, as the shared layer needs to see it.
    /// </summary>
    /// <remarks>
    /// The on-screen pause button and the WebGL focus-loss handler both have to
    /// drive pause, but neither belongs to any one game. Games implement this on
    /// their own pause menu; shared code finds it by interface, so a game can be
    /// unloaded without leaving a dangling type reference behind.
    /// </remarks>
    public interface IPauseController
    {
        bool IsPaused { get; }

        void Pause();

        void Resume();

        void TogglePause();
    }
}
