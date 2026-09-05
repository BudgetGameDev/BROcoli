namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>Everything the agent knows this tick, flattened for pure scoring.</summary>
    internal readonly struct BotSituation
    {
        internal readonly bool HasEnemies;
        internal readonly float NearestEnemyDistance;
        internal readonly int CloseEnemyCount;
        internal readonly float HealthFraction;
        internal readonly bool IncomingProjectile;
        internal readonly bool Recovering;

        /// <summary>Ground distance to the nearest unopened chest, or infinity.</summary>
        internal readonly float ChestDistance;

        /// <summary>Ground distance to the nearest loose pickup, or infinity.</summary>
        internal readonly float PickupDistance;

        /// <summary>
        /// True once fighting has achieved nothing for a while: no experience, no
        /// enemy down, no damage taken. Something is in sense range that cannot be
        /// reached or hurt -- across a wall, or in a room that never woke up.
        /// </summary>
        internal readonly bool EngagementStalled;

        /// <summary>
        /// How much of the space around the agent has something in it, from nothing to
        /// completely enclosed. Counting bodies says how many; this says whether they
        /// are on one side or all of them, which is the difference between a fight to
        /// back out of and one to break out of.
        /// </summary>
        internal readonly float Encirclement;

        internal BotSituation(
            bool hasEnemies,
            float nearestEnemyDistance,
            int closeEnemyCount,
            float healthFraction,
            bool incomingProjectile,
            bool recovering,
            float chestDistance,
            float pickupDistance,
            bool engagementStalled = false,
            float encirclement = 0f
        )
        {
            EngagementStalled = engagementStalled;
            Encirclement = encirclement;
            HasEnemies = hasEnemies;
            NearestEnemyDistance = nearestEnemyDistance;
            CloseEnemyCount = closeEnemyCount;
            HealthFraction = healthFraction;
            IncomingProjectile = incomingProjectile;
            Recovering = recovering;
            ChestDistance = chestDistance;
            PickupDistance = pickupDistance;
        }
    }

    /// <summary>
    /// The thresholds the scoring reads. Bundled so the policy stays a two-argument
    /// pure function as goals are added, rather than growing a parameter per knob.
    /// </summary>
    internal readonly struct BotTuning
    {
        internal readonly float DangerRadius;
        internal readonly int CrowdCount;
        internal readonly float LowHealthFraction;
        internal readonly float SenseRadius;

        /// <summary>How far away a chest or pickup is still worth walking to.</summary>
        internal readonly float ObjectiveRadius;

        internal BotTuning(
            float dangerRadius,
            int crowdCount,
            float lowHealthFraction,
            float senseRadius,
            float objectiveRadius
        )
        {
            DangerRadius = dangerRadius;
            CrowdCount = crowdCount;
            LowHealthFraction = lowHealthFraction;
            SenseRadius = senseRadius;
            ObjectiveRadius = objectiveRadius;
        }
    }
}
