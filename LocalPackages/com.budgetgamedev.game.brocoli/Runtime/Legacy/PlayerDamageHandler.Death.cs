using System.Collections;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class PlayerDamageHandler
    {
        /// <summary>
        /// Trigger game over state.
        /// </summary>
        public void TriggerGameOver()
        {
            if (_gameOver)
                return;

            Debug.Log("Game over");
            _gameOver = true;
            BrocoliSaveSystem.DeleteActiveSave();

            // Stop ambient audio
            _audioHandler?.StopAllAmbient();
            _audioHandler?.PlayDeathSound();

            // Save and display the final run state without loading another scene.
            SaveFinalRunStats(
                out int finalScore,
                out int finalRooms,
                out int finalEnemiesKilled,
                out float finalTimeSurvived
            );

            // Notify listeners
            OnGameOver?.Invoke();

            StopPlayerSimulation();
            StartCoroutine(
                PlayDeathSequence(finalScore, finalRooms, finalEnemiesKilled, finalTimeSurvived)
            );
        }

        private void StopPlayerSimulation()
        {
            foreach (Collider playerCollider in GetComponents<Collider>())
                playerCollider.enabled = false;

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.SetSimulated(false);
            }

            _deathVisual?.Prepare();
        }

        private IEnumerator PlayDeathSequence(
            int score,
            int rooms,
            int enemiesKilled,
            float timeSurvived
        )
        {
            _deathAnimationPlaying = true;
            if (_deathVisual != null)
                yield return _deathVisual.FallAndSettle(deathFallDuration, deathSettleDuration);
            else
                yield return new WaitForSecondsRealtime(DeathAnimationDuration);
            _deathAnimationPlaying = false;
            GameOverOverlay.Show(score, rooms, enemiesKilled, timeSurvived);
        }

        private void SaveFinalRunStats(
            out int score,
            out int rooms,
            out int enemiesKilled,
            out float timeSurvived
        )
        {
            GameStates gameStates = FindAnyObjectByType<GameStates>();
            DungeonManager dungeon = FindAnyObjectByType<DungeonManager>();
            score = gameStates != null ? gameStates.score : 0;
            rooms = dungeon != null ? dungeon.RoomsVisited : 0;
            enemiesKilled = gameStates != null ? gameStates.EnemiesKilled : 0;
            timeSurvived = gameStates != null ? gameStates.gameTime : 0f;

            PlayerPrefs.SetInt("LastScore", score);
            PlayerPrefs.SetInt("LastRooms", rooms);
            PlayerPrefs.SetInt("LastEnemiesKilled", enemiesKilled);
            PlayerPrefs.SetFloat("LastTimeSurvived", timeSurvived);
            PlayerPrefs.Save();
            Debug.Log(
                $"Saved final run: score {score}, rooms {rooms}, "
                    + $"enemies {enemiesKilled}, time {GameStates.FormatSurvivalTime(timeSurvived)}"
            );
        }
    }
}
