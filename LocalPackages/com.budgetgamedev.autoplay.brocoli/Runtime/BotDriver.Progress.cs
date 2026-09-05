using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
        /// <summary>
        /// Marks the last moment combat achieved anything, which is only ever an
        /// enemy going down. Health regeneration ticks forever and enemies wander in
        /// and out of sense range, so neither can count.
        ///
        /// Nor can being hit, however much it feels like a fight. An agent holding
        /// its weapon's range against a crowd it is not killing takes a hit every
        /// few seconds, and reading that as progress is what let a run spend a whole
        /// game-minute walking the same circle while fifty enemies followed it
        /// round: nothing was landing that ended anything, and nothing noticed.
        ///
        /// The kill ledger is read rather than the experience bar because
        /// experience is granted where the orb is picked up rather than where the
        /// enemy died, so a run that is killing but not collecting reads as stalled.
        /// </summary>
        private void TrackCombatProgress()
        {
            int kills = AutoplayFeatureLog.Count(AutoplayFeatures.EnemyKilled);
            bool killing = kills > lastKills;

            lastKills = kills;
            if (killing)
            {
                lastProgress = Time.time;
                lastCombatProgress = Time.time;
            }
        }

        /// <summary>Notices doorway crossings, which is the only place they are observable.</summary>
        private void TrackRoom(Vector2 position)
        {
            Vector2Int room = DungeonLayout.RoomAt(position);
            if (hasOccupiedRoom && room == occupiedRoom)
                return;

            rejectedExits.Clear();
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
            lastCombatProgress = Time.time;
            recoveriesSinceProgress = 0;
        }

        private void TrackProgress(Vector2 position)
        {
            if (!hasPosition)
            {
                lastPosition = position;
                lastProgressPosition = position;
                loiterOrigin = position;
                nextProgressCheck = Time.time + progressCheckInterval;
                nextLoiterCheck = Time.time + loiterWindow;
                hasPosition = true;
                return;
            }

            float travelled = Vector2.Distance(position, lastPosition);
            DistanceTravelled += travelled;
            lastPosition = position;
            TrackLoitering(position, travelled);
            if (Time.time < nextProgressCheck)
                return;

            float elapsed = Mathf.Max(progressCheckInterval, Time.time - nextProgressCheck);
            nextProgressCheck = Time.time + progressCheckInterval;
            float progress = Vector2.Distance(position, lastProgressPosition);
            if ((Move.sqrMagnitude > 0.2f || MovementBlocked) && progress < 0.12f)
                stationaryTime += elapsed;
            else
                stationaryTime = 0f;

            if (stationaryTime >= stuckRecoveryDelay)
                BeginStuckRecovery();
            lastProgressPosition = position;
        }

        /// <summary>
        /// Notices walking that goes nowhere. An agent pacing a wall or circling
        /// the middle of a room is moving the whole time, so the stationary check
        /// never fires and the only backstop is half a game-minute of reaching no
        /// new room -- which is most of a short run spent shuffling. Comparing how
        /// far it walked with how far it actually got catches both in seconds.
        ///
        /// Only the goals that are meant to take the agent somewhere are judged.
        /// Fighting is circling on purpose: an agent kiting at the edge of its
        /// weapon's range covers ground without meaning to leave, and reading that
        /// as being stuck would make it abandon every fight it was winning.
        /// </summary>
        private void TrackLoitering(Vector2 position, float travelled)
        {
            if (!IsJourney(currentIntent) || Time.time < recoveryUntil)
            {
                loiterTravelled = 0f;
                loiterOrigin = position;
                nextLoiterCheck = Time.time + loiterWindow;
                return;
            }

            loiterTravelled += travelled;
            if (Time.time < nextLoiterCheck)
                return;

            bool loitering = IsLoitering(
                loiterTravelled,
                Vector2.Distance(position, loiterOrigin),
                loiterEfficiency
            );
            loiterTravelled = 0f;
            loiterOrigin = position;
            nextLoiterCheck = Time.time + loiterWindow;
            if (!loitering)
                return;

            // Walking a long way to end up where it started is not one bad step to
            // shuffle out of, it is a destination the agent cannot reach from here.
            // Write it off at once rather than after four more manoeuvres.
            BeginStuckRecovery();
            AbandonCurrentTarget();
        }

        /// <summary>Goals whose whole point is arriving somewhere else.</summary>
        internal static bool IsJourney(BotIntent intent) =>
            intent is BotIntent.Explore or BotIntent.Loot or BotIntent.Collect;

        /// <summary>
        /// Whether a stretch of walking went anywhere: the net displacement has to
        /// be at least <paramref name="efficiency"/> of the distance covered. Too
        /// short a stretch is not judged at all -- barely moving is the stationary
        /// check's question, and answering it here would fire on a single tick
        /// spent turning around.
        /// </summary>
        internal static bool IsLoitering(float travelled, float displacement, float efficiency) =>
            travelled >= MinimumJudgeableTravel && displacement < travelled * efficiency;

        private void BeginStuckRecovery()
        {
            TraceRecoveryCandidates();
            // Wedged in a crowd is not wedged in the geometry, and the manoeuvre for
            // one is useless against the other. Enemy bodies are deliberately not
            // navigation obstacles -- the agent has to be able to path through where
            // one is standing -- so a ring of them reads as open floor in every
            // direction while physically holding the agent still. A run measured
            // eighty game-seconds at a single unchanged position with sixty-six of
            // them around it, collecting an unsticking manoeuvre every second. When
            // there is a gap in the ring, that gap is the manoeuvre.
            if (lastEscape.sqrMagnitude > 0.001f)
            {
                recoveryDirection = lastEscape.normalized;
                recoveryUntil = Time.time + 0.8f;
                stationaryTime = 0f;
                nextPathRefresh = 0f;
                StuckRecoveryCount++;
                if (++recoveriesSinceProgress >= recoveriesBeforeAbandoning)
                    AbandonCurrentTarget();
                return;
            }

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
            else if (currentIntent == BotIntent.Explore)
                rejectedExits.Add(explorationRoom);

            hasExplorationRoom = false;
            explorationDirection = -1;
            objectives = ObjectiveObservation.None;
            nextObjectiveScan = Time.time + abandonedObjectiveDelay;
            recoveriesSinceProgress = 0;
        }
    }
}
