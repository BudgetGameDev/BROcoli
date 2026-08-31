namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Stands in for a game's pause screen. The shared layer only ever reaches a
    /// pause menu through <see cref="IPauseController"/>, so a test needs nothing
    /// but an implementation of it - no scene, and no component.
    /// </summary>
    public sealed class TestPauseController : IPauseController
    {
        public int PauseCalls { get; private set; }

        public int ResumeCalls { get; private set; }

        public bool IsPaused { get; private set; }

        public void Pause()
        {
            PauseCalls++;
            IsPaused = true;
        }

        public void Resume()
        {
            ResumeCalls++;
            IsPaused = false;
        }

        public void TogglePause()
        {
            if (IsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }
}
