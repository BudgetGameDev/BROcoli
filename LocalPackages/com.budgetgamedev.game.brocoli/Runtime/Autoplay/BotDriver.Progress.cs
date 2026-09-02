using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
        /// <summary>
        /// Marks the last moment combat achieved anything. Only two things count:
        /// earning experience, and being hit. Health regeneration ticks forever and
        /// enemies wander in and out of sense range, so treating either as progress
        /// keeps resetting the clock and the agent never notices it is up against
        /// something it cannot reach.
        /// </summary>
        private void TrackCombatProgress()
        {
            float experience = stats != null ? stats.CurrentExperience : 0f;
            float health = stats != null ? stats.CurrentHealth : 0f;
            bool fighting = experience > lastExperience + 0.01f || health < lastHealth - 0.01f;

            lastExperience = experience;
            lastHealth = health;
            if (fighting)
                lastProgress = Time.time;
        }

        /// <summary>Notices doorway crossings, which is the only place they are observable.</summary>
        private void TrackRoom(Vector2 position)
        {
            Vector2Int room = DungeonLayout.RoomAt(position);
            if (hasOccupiedRoom && room == occupiedRoom)
                return;

            bool discovered = visitedRooms.Add(room);
            if (hasOccupiedRoom)
                AutoplayFeatureLog.Record(AutoplayFeatures.DoorTraversed);
            if (discovered)
                AutoplayFeatureLog.Record(AutoplayFeatures.RoomEntered);

            occupiedRoom = room;
            hasOccupiedRoom = true;
            if (!discovered)
                return;

            lastProgress = Time.time;
            recoveriesSinceProgress = 0;
        }

        private void TrackProgress(Vector2 position)
        {
            if (!hasPosition)
            {
                lastPosition = position;
                lastProgressPosition = position;
                nextProgressCheck = Time.time + progressCheckInterval;
                hasPosition = true;
                return;
            }

            float travelled = Vector2.Distance(position, lastPosition);
            DistanceTravelled += travelled;
            lastPosition = position;
            if (Time.time < nextProgressCheck)
                return;

            float elapsed = Mathf.Max(progressCheckInterval, Time.time - nextProgressCheck);
            nextProgressCheck = Time.time + progressCheckInterval;
            float progress = Vector2.Distance(position, lastProgressPosition);
            if (Move.sqrMagnitude > 0.2f && progress < 0.12f)
                stationaryTime += elapsed;
            else
                stationaryTime = 0f;

            if (stationaryTime >= stuckRecoveryDelay)
                BeginStuckRecovery();
            lastProgressPosition = position;
        }

        private void BeginStuckRecovery()
        {
            Vector2 basis = Move.sqrMagnitude > 0.01f ? Move.normalized : Vector2.up;
            recoveryDirection = Vector2.Perpendicular(basis) * recoverySide - basis * 0.25f;
            recoveryDirection.Normalize();
            recoverySide = -recoverySide;
            recoveryUntil = Time.time + 0.8f;
            stationaryTime = 0f;
            nextPathRefresh = 0f;
            StuckRecoveryCount++;

            if (++recoveriesSinceProgress >= recoveriesBeforeAbandoning)
                AbandonCurrentTarget();
        }

        /// <summary>
        /// Unsticking itself several times without reaching a new room means the
        /// destination is not reachable from here -- a doorway that does not connect,
        /// a chest across a wall. Write it off and pick something else, or the agent
        /// spends the rest of the run shuffling against the same geometry.
        /// </summary>
        private void AbandonCurrentTarget()
        {
            // Nothing left to write off means the agent is not choosing badly, it is
            // physically wedged. The middle of the room it is standing in is the one
            // place always on the navmesh, so aim there before trying to leave again.
            if (!hasExplorationRoom)
                unwedgeUntil = Time.time + unwedgeSeconds;
            else
                visitedRooms.Add(explorationRoom);

            hasExplorationRoom = false;
            explorationDirection = -1;
            objectives = ObjectiveObservation.None;
            nextObjectiveScan = Time.time + abandonedObjectiveDelay;
            recoveriesSinceProgress = 0;
        }
    }
}
