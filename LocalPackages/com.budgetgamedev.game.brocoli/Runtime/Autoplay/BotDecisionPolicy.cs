using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    internal enum BotIntent
    {
        Waiting,
        Explore,
        Engage,
        Retreat,
        Dodge,
        Recover,
        Loot,
        Collect,
    }

    /// <summary>
    /// Utility scoring over the agent's competing goals. Every goal is scored on the
    /// same scale each tick and the best one wins, so a chest worth grabbing can
    /// outrank a distant enemy without either being hard-coded as more important.
    ///
    /// Two goals stay absolute rather than scored. Recovery is a bounded manoeuvre
    /// that only works if it is allowed to finish, and a projectile already in the
    /// air gives no second chance -- deliberating over either is how the agent gets
    /// stuck or shot.
    ///
    /// The scores are calibrated so that any one retreat trigger -- something inside
    /// the danger radius, a crowd, or low health -- outranks the most enthusiastic
    /// possible urge to attack. Blending those into a single number is what lets the
    /// agent fight at the edge of its weapon's range instead of alternating between
    /// charging and fleeing.
    /// </summary>
    internal static class BotDecisionPolicy
    {
        /// <summary>Utility a goal must beat to displace the goal already running.</summary>
        internal const float Hysteresis = 6f;

        /// <summary>The most an attack urge can ever be worth; retreats clear it.</summary>
        private const float EngageCeiling = 56f;

        /// <summary>
        /// Share of the surrounding space with something in it before the agent treats
        /// itself as being closed in on. Deliberately short of a full ring: a crowd
        /// that has three sides of the agent is one step from having four, and the
        /// whole value of noticing is noticing while there is still a way out.
        /// </summary>
        internal const float CrowdingConcern = 0.45f;

        private static readonly BotIntent[] Scored =
        {
            BotIntent.Explore,
            BotIntent.Engage,
            BotIntent.Retreat,
            BotIntent.Loot,
            BotIntent.Collect,
        };

        internal static BotIntent ChooseIntent(
            BotSituation situation,
            BotTuning tuning,
            BotIntent previous
        )
        {
            if (situation.Recovering)
                return BotIntent.Recover;
            if (situation.IncomingProjectile)
                return BotIntent.Dodge;

            BotIntent best = BotIntent.Explore;
            float bestScore = float.NegativeInfinity;
            foreach (BotIntent candidate in Scored)
            {
                float score = Utility(candidate, situation, tuning);
                if (candidate == previous)
                    score += Hysteresis;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Scores one goal. Returns negative infinity when the goal has nothing to
        /// act on, which is what keeps an absent chest from ever being chosen.
        /// </summary>
        internal static float Utility(BotIntent intent, BotSituation situation, BotTuning tuning) =>
            intent switch
            {
                BotIntent.Explore => 30f - situation.CloseEnemyCount * 3f,
                BotIntent.Engage => EngageUtility(situation, tuning),
                BotIntent.Retreat => RetreatUtility(situation, tuning),
                BotIntent.Loot => ObjectiveUtility(situation.ChestDistance, 70f, situation, tuning),
                BotIntent.Collect => CollectUtility(situation, tuning),
                _ => float.NegativeInfinity,
            };

        private static float EngageUtility(BotSituation situation, BotTuning tuning)
        {
            // A standoff with something unreachable would otherwise outscore
            // exploring forever, and the agent spends the whole run pacing at a wall.
            if (!situation.HasEnemies || situation.EngagementStalled)
                return float.NegativeInfinity;

            // Confidence: a healthy agent presses, a hurt one wants a better reason.
            float score = 26f + situation.HealthFraction * 20f;
            score += Proximity(situation.NearestEnemyDistance, tuning.SenseRadius) * 10f;
            if (situation.CloseEnemyCount >= tuning.CrowdCount)
                score -= 18f;
            // Standing and fighting is worth less the more of the room is behind the
            // agent rather than in front of it.
            if (situation.Encirclement >= CrowdingConcern)
                score -= 20f * situation.Encirclement;
            return score;
        }

        private static float RetreatUtility(BotSituation situation, BotTuning tuning)
        {
            if (!situation.HasEnemies)
                return float.NegativeInfinity;

            // A stalled standoff the agent is walking away from untouched has nothing
            // to back away from, and backing away and returning is the other half of
            // the pacing this is here to stop. Two things are not that. Being hurt in
            // one is a fight going nowhere while the agent bleeds, which is exactly
            // the fight to leave; and something already inside the danger radius is
            // biting, whatever the stall clock says. Without the second, writing off a
            // fight sends the agent walking through the crowd it just gave up on.
            if (
                situation.EngagementStalled
                && situation.HealthFraction >= tuning.LowHealthFraction
                && situation.NearestEnemyDistance >= tuning.DangerRadius
            )
                return float.NegativeInfinity;

            float score = 12f;
            if (situation.NearestEnemyDistance < tuning.DangerRadius)
                score += 50f + 25f * Proximity(situation.NearestEnemyDistance, tuning.DangerRadius);
            if (situation.CloseEnemyCount >= tuning.CrowdCount)
                score += 48f;
            // Being closed in on is worth acting on before it finishes: a crowd that
            // has most of the way round the agent is the last moment there is a gap
            // left to leave through.
            if (situation.Encirclement >= CrowdingConcern)
                score += 30f + 40f * situation.Encirclement;
            if (situation.HealthFraction < tuning.LowHealthFraction)
                score += 30f + 40f * (1f - situation.HealthFraction / tuning.LowHealthFraction);
            return score;
        }

        /// <summary>
        /// Experience earns the same floor a chest does: an orb anywhere in sight has
        /// to outrank wandering off. It used to sit below that, on the grounds that a
        /// distant orb was not worth abandoning exploration for -- true while orbs
        /// expired half a minute after they dropped, and false now they wait on the
        /// floor. A run that fights through a room and leaves its far corner behind is
        /// throwing away experience it has already earned, and levelling is measured
        /// on what the run collects rather than on what it killed.
        /// </summary>
        private static float CollectUtility(BotSituation situation, BotTuning tuning)
        {
            float score = ObjectiveUtility(situation.PickupDistance, 68f, situation, tuning);
            // A dropped boost is worth a detour precisely when the run is going badly.
            return situation.HealthFraction < tuning.LowHealthFraction ? score + 14f : score;
        }

        /// <summary>
        /// Shared shape for "walk to a thing on the floor". The floor under the
        /// proximity term is deliberate: a chest anywhere in sight has to outrank
        /// wandering off, or the required loot path only gets tested by luck.
        /// </summary>
        private static float ObjectiveUtility(
            float distance,
            float baseScore,
            BotSituation situation,
            BotTuning tuning
        )
        {
            if (float.IsPositiveInfinity(distance))
                return float.NegativeInfinity;

            float score = baseScore * (0.45f + 0.55f * Proximity(distance, tuning.ObjectiveRadius));
            if (situation.HasEnemies && situation.NearestEnemyDistance < tuning.DangerRadius)
                score -= EngageCeiling;
            return score;
        }

        /// <summary>1 when the target is underfoot, falling to 0 at <paramref name="range"/>.</summary>
        internal static float Proximity(float distance, float range)
        {
            if (range <= 0f || float.IsPositiveInfinity(distance))
                return 0f;
            return Mathf.Clamp01(1f - distance / range);
        }
    }
}
