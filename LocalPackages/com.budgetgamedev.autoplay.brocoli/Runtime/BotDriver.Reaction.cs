using BudgetGameDev.Autoplay;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
        internal readonly struct TacticalSnapshot
        {
            internal readonly BotSituation Situation;
            internal readonly EnemyObservation Enemies;
            internal readonly ObjectiveObservation Objectives;
            internal readonly Vector2 Dodge;

            internal TacticalSnapshot(
                BotSituation situation,
                EnemyObservation enemies,
                ObjectiveObservation objectives,
                Vector2 dodge
            )
            {
                Situation = situation;
                Enemies = enemies;
                Objectives = objectives;
                Dodge = dodge;
            }
        }

        private DelayedCommandScheduler<TacticalSnapshot> reaction = new(0, 0);
        private TacticalSnapshot activeTactics;
        private ObjectiveObservation observedObjectives;
        private Vector2 observedDodge;
        private bool stressReaction = true;
        private BotIntent tacticalIntent = BotIntent.Waiting;
        private static BotDriver reactionOwner;
        private static long previousObservations;
        private static long previousDecisions;
        public static long ReactionObservationCount =>
            previousObservations
            + (reactionOwner != null ? reactionOwner.reaction.ObservationCount : 0);
        public static long ReactionDecisionCount =>
            previousDecisions
            + (reactionOwner != null ? reactionOwner.reaction.ActivationCount : 0);

        internal void ConfigureReaction(AutoplayConfig config)
        {
            stressReaction =
                config.ObservationIntervalSeconds == 0f && config.ReactionDelaySeconds == 0f;
            reaction = new DelayedCommandScheduler<TacticalSnapshot>(
                config.ObservationIntervalSeconds,
                config.ReactionDelaySeconds
            );
            ResetReactionCounters();
            ResetReactionLife();
        }

        private void ResetReactionCounters()
        {
            reactionOwner = this;
            previousObservations = 0;
            previousDecisions = 0;
            reaction.Reset();
        }

        private void ResetReactionLife()
        {
            previousObservations += reaction.ObservationCount;
            previousDecisions += reaction.ActivationCount;
            reaction.Reset();
            activeTactics = default;
            observedObjectives = default;
            observedDodge = Vector2.zero;
            tacticalIntent = BotIntent.Waiting;
        }

        private void UpdateTactics(Vector2 position) => AdvanceTactics(position, Time.timeAsDouble);

        internal void AdvanceTactics(Vector2 position, double now)
        {
            if (reaction.TryObserve(now))
                reaction.Enqueue(CaptureTactics(position), now);
            if (!reaction.TryActivate(now, out TacticalSnapshot snapshot))
                return;

            activeTactics = snapshot;
            objectives = snapshot.Objectives;
            if (objectives.HasPickup && objectiveProgress.IsRetired(objectives.Pickup, Time.time))
                objectives = new ObjectiveObservation(
                    objectives.HasChest,
                    objectives.Chest,
                    false,
                    default
                );
            if (objectives.HasChest && objectiveProgress.IsRetired(objectives.Chest, Time.time))
                objectives = new ObjectiveObservation(
                    false,
                    default,
                    objectives.HasPickup,
                    objectives.Pickup
                );
            lastDodge = snapshot.Dodge;
            lastEscape =
                snapshot.Enemies.Coverage >= EncirclementBreakout
                    ? snapshot.Enemies.Escape
                    : Vector2.zero;
            tacticalIntent = BotDecisionPolicy.ChooseIntent(
                snapshot.Situation,
                Tuning,
                tacticalIntent
            );
            if (
                (tacticalIntent == BotIntent.Collect && !objectives.HasPickup)
                || (tacticalIntent == BotIntent.Loot && !objectives.HasChest)
            )
                tacticalIntent = BotIntent.Explore;
        }

        private TacticalSnapshot CaptureTactics(Vector2 position)
        {
            // ObserveEnemies also computes escape geometry; do not leak that fresh
            // observation into recovery before this snapshot's reaction delay ends.
            Vector2 previousEscape = lastEscape;
            EnemyObservation enemies = ObserveEnemies(position);
            lastEscape = previousEscape;
            if (!stressReaction || ++frame % 3 == 0)
                observedDodge = ComputeProjectileDodge(position);
            Vector2 dodge = observedDodge;
            if (Time.time >= nextObjectiveScan)
            {
                observedObjectives = ObserveObjectives(position);
                nextObjectiveScan = Time.time + objectiveScanInterval;
            }
            ObjectiveObservation seenObjectives = observedObjectives;
            float health =
                stats != null && stats.CurrentMaxHealth > 0f
                    ? stats.CurrentHealth / stats.CurrentMaxHealth
                    : 1f;
            var situation = new BotSituation(
                enemies.Count > 0,
                enemies.NearestDistance,
                enemies.CloseCount,
                health,
                dodge.sqrMagnitude > 0.0001f,
                false,
                seenObjectives.ChestDistance(position),
                seenObjectives.PickupDistance(position),
                Time.time - lastCombatProgress > engagementStallDelay,
                enemies.Coverage
            );
            return new TacticalSnapshot(situation, enemies, seenObjectives, dodge);
        }
    }
}
