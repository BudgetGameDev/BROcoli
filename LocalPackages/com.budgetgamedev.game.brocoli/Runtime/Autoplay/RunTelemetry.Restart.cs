namespace BudgetGameDev.Games.Brocoli
{
    public partial class RunTelemetry
    {
        /// <summary>
        /// A roguelite run ends in death. The scenarios above start another one
        /// instead -- which is also the only thing that ever presses the game-over
        /// screen's own restart button.
        /// </summary>
        private void OnGameOver()
        {
            _progression.NoteDeath();
            if (PlaysAnotherLife && _elapsed < _cfg.Duration)
            {
                _awaitingRestart = true;
                return;
            }

            EndRun("gameover");
        }

        /// <summary>
        /// Presses restart once the overlay is actually up, which is a moment later
        /// than the death itself: the death animation runs first.
        /// </summary>
        private bool TryRestart()
        {
            GameOverOverlay overlay = GameOverOverlay.Active;
            if (overlay == null || !overlay.IsVisible)
                return false;

            _awaitingRestart = false;
            if (_damage != null)
                _damage.OnGameOver -= OnGameOver;
            _stats = null;
            _damage = null;
            _dungeon = null;
            _lastProgressTime = _elapsed;
            PressRestart(overlay);
            return true;
        }
    }
}
