using System;
using System.IO;
using System.Text;
using BudgetGameDev.Autoplay;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class RunTelemetry
    {
        /// <summary>
        /// How far out of the dungeon the player currently is. Read from where they
        /// are standing rather than from what the dungeon has streamed in, because
        /// rooms are built ahead of arrival and depth is about where the run got to.
        /// </summary>
        private int PlayerRing =>
            _dungeon != null && _dungeon.HasCurrentRoom
                ? DungeonLayout.Ring(_dungeon.CurrentRoom)
                : 0;

        /// <summary>Remembers the last moment the run actually got somewhere.</summary>
        private void TrackProgress()
        {
            float level = _stats != null ? _stats.CurrentLevel : 0f;
            float experience = _stats != null ? _stats.CurrentExperience : 0f;
            int rooms = _dungeon != null ? _dungeon.RoomsVisited : 0;
            if (
                Mathf.Approximately(level, _progressLevel)
                && Mathf.Approximately(experience, _progressExperience)
                && rooms == _progressRooms
            )
                return;

            _progressLevel = level;
            _progressExperience = experience;
            _progressRooms = rooms;
            _lastProgressTime = _elapsed;
        }

        /// <summary>
        /// Whether a journey run has been everywhere it set out to go. It is graded
        /// on its steps rather than on its length, so the minutes after the last one
        /// would be nothing but the bot playing on -- and the run's own subject, a
        /// death, has already happened by then.
        /// </summary>
        private bool JourneyIsOver =>
            _cfg.Scenario == AutoplayFeatures.JourneyScenario
            && AutoplayFeatureLog.Missing(AutoplayFeatures.SaveJourney).Count == 0;

        /// <summary>
        /// Scenarios that read a whole session rather than one life. A coverage sweep
        /// that stopped at the first death would only ever test whatever that life
        /// stumbled into, and a difficulty verdict drawn from a single life is a
        /// verdict on that life's luck.
        /// </summary>
        private bool PlaysAnotherLife =>
            _cfg.Scenario == "coverage"
            || _cfg.Scenario == "balance"
            || _cfg.Scenario == AutoplayFeatures.JourneyScenario;

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
