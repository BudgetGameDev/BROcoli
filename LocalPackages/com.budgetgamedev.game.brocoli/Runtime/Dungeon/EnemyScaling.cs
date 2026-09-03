using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// How hard the dungeon sets a room, kept in one place so difficulty is tuned by
    /// editing numbers rather than by hunting through the spawn path. This is the
    /// enemy side of what <see cref="PlayerProgression"/> is for the player.
    ///
    /// A room is scaled twice over. Depth raises enemy health with the ring, and the
    /// player's own power score (see <see cref="PlayerStats.ComputePowerScore"/>)
    /// feeds back into health, damage, and headcount. Every power exponent sits below
    /// one so upgrades still feel like progress: a player four times as powerful meets
    /// enemies roughly twice as tough, not four times.
    ///
    /// Damage is scaled harder than health on purpose. Answering a stronger player
    /// only with deeper health pools makes every fight longer without making any of it
    /// dangerous, which measures as a slog rather than as difficulty. A sweep with the
    /// two the other way round bore that out: enemy health had grown 2.7x by the
    /// deepest room while their damage had managed 1.6x, so the deep rooms took longer
    /// and threatened no more. The two exponents still sum to what they did, so the
    /// dungeon answers the player exactly as hard as before -- it now answers with the
    /// half of the answer the player can feel.
    ///
    /// The caps are the ceiling a run can push scaling to. A run that spends most of
    /// its rooms pinned against one has stopped scaling, which the balance verdict
    /// reports as lost headroom rather than leaving it to be noticed by eye.
    /// </summary>
    public static class EnemyScaling
    {
        /// <summary>
        /// Extra enemy health per ring beyond the first. The slope is gentle because
        /// depth already changes the fight twice over: the ring ladder unlocks
        /// archetypes with many times the health of the first ring's, and player power
        /// feeds back on top. Stacking a steep third multiplier on those made a
        /// wandering run walk into rooms it could not damage, which measures as a
        /// stalled run rather than a hard one.
        /// </summary>
        public const float DepthHealthSlope = 0.09f;

        /// <summary>How much of the player's power growth feeds back into enemy health.</summary>
        public const float HealthPowerExponent = 0.45f;

        /// <summary>And into the damage they deal, which is what makes a room dangerous.</summary>
        public const float DamagePowerExponent = 0.52f;

        /// <summary>And into how many of them a room holds.</summary>
        public const float CountPowerExponent = 0.25f;

        /// <summary>
        /// Extra enemy pace per ring beyond the first, and per point of player power.
        ///
        /// Every melee archetype is authored slower than the player -- two to two and
        /// a half against four -- so a player who keeps walking simply cannot be
        /// caught by any of them, and a balance sweep measured ninety-two per cent
        /// mean health with nothing else out of band. Depth closes that gap rather
        /// than the prefabs, so the first ring stays as forgiving as it was authored
        /// and the tenth is somewhere the player has to deal with what is chasing
        /// them. Nothing is ever scaled past <see cref="SpeedCeiling"/>: an enemy
        /// faster than the player is not difficulty, it is an unavoidable hit.
        /// </summary>
        public const float DepthSpeedSlope = 0.05f;
        public const float SpeedPowerExponent = 0.12f;
        public const float MaxSpeedScale = 1.6f;

        /// <summary>
        /// The fastest depth may make anything, just under the player's own four, so
        /// the ladder crowds a walking player without ever making a hit unavoidable.
        /// </summary>
        public const float SpeedCeiling = 3.9f;

        /// <summary>
        /// The spider's own ceiling, and the one thing in the dungeon allowed past the
        /// player. It is the run's skirmisher: the archetype whose whole job is that
        /// walking away from it does not work, so that the player has to answer it
        /// with the build rather than with the stick.
        /// </summary>
        public const float SpiderSpeedCeiling = 6.5f;

        /// <summary>Ceilings, so a runaway build cannot be answered with an unkillable room.</summary>
        public const float MaxHealthPowerScale = 6f;
        public const float MaxDamagePowerScale = 3.2f;
        public const float MaxCountPowerScale = 1.75f;

        /// <summary>The health multiplier a ring carries on its own.</summary>
        public static float Depth(int ring) => 1f + DepthHealthSlope * Mathf.Max(0, ring - 1);

        /// <summary>The health multiplier the player's own power earns them.</summary>
        public static float Health(float power) =>
            PowerScale(power, HealthPowerExponent, MaxHealthPowerScale);

        /// <summary>The damage multiplier it earns them.</summary>
        public static float Damage(float power) =>
            PowerScale(power, DamagePowerExponent, MaxDamagePowerScale);

        /// <summary>And the headcount multiplier.</summary>
        public static float Count(float power) =>
            PowerScale(power, CountPowerExponent, MaxCountPowerScale);

        /// <summary>How much faster a room's enemies are for how deep and how late it is.</summary>
        public static float SpeedScale(int ring, float power) =>
            Mathf.Min(
                MaxSpeedScale,
                (1f + DepthSpeedSlope * Mathf.Max(0, ring - 1))
                    * PowerScale(power, SpeedPowerExponent, MaxSpeedScale)
            );

        /// <summary>
        /// How each archetype carries itself, before depth touches it. The ring ladder
        /// (see <see cref="DungeonEnemyPlacer.MinRingFor"/>) decides when the player
        /// meets a shape; this decides what meeting it feels like, and the two are
        /// read together.
        ///
        /// Spiders skirmish and everything about them is speed. The shooting
        /// archetypes want a firing line rather than a chase, so they are the slowest
        /// thing in the dungeon and hang back. The hydra lumbers, a little under the
        /// melee line, because a splitting enemy that also kept pace would be two
        /// problems at once. Otherwise weight decides it: the chunky tiers are the
        /// slabs the player walks around, and the small ones are quick.
        /// </summary>
        public static float ArchetypePace(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return 1f;
            if (prefabName.Contains("Spider"))
                return 1.4f;
            // Before the weight rules: "ShootingHard" is a shooter, not a hard melee.
            if (prefabName.Contains("Shooting"))
                return 0.8f;
            if (prefabName.Contains("Hydra"))
                return 0.9f;
            // And before "Easy" or "Normal": "EnemyEasyChunky" is a slab.
            if (prefabName.Contains("Chunky"))
                return 0.85f;
            if (prefabName.Contains("Easy") || prefabName.Contains("Normal"))
                return 1.05f;
            return 1f;
        }

        /// <summary>How fast this archetype is ever allowed to become.</summary>
        public static float PaceCeiling(string prefabName) =>
            !string.IsNullOrEmpty(prefabName) && prefabName.Contains("Spider")
                ? SpiderSpeedCeiling
                : SpeedCeiling;

        /// <summary>
        /// The pace one enemy is actually built with: what it was authored at, bent to
        /// its archetype's character, then carried by depth up to its own ceiling.
        /// </summary>
        public static float Speed(float authored, float scale, string prefabName)
        {
            float paced = Mathf.Max(0f, authored) * ArchetypePace(prefabName);
            return Mathf.Max(paced, Mathf.Min(paced * scale, PaceCeiling(prefabName)));
        }

        /// <summary>Whether a scale has run out of room to answer a stronger player.</summary>
        internal static bool AtCap(float scale, float max) => scale >= max - 0.001f;

        /// <summary>A power multiplier that never weakens enemies below baseline.</summary>
        private static float PowerScale(float power, float exponent, float max) =>
            Mathf.Clamp(Mathf.Pow(Mathf.Max(1f, power), exponent), 1f, max);
    }
}
