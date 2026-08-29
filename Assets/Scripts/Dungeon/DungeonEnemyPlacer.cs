using System.Collections.Generic;
using Pooling;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns a room's enemies ahead of the player's arrival. Enemies come from
/// the shared <see cref="PoolManager"/> and stand visibly in the room but
/// dormant (their AI component disabled), so there is no pop-in when the
/// player peers through a doorway; entering the room wakes the whole group.
/// </summary>
public static class DungeonEnemyPlacer
{
    private const float CenterClearRadius = 4f;

    // How much of the player's power growth (see PlayerStats.ComputePowerScore)
    // feeds back into enemy strength. Every exponent sits below 1 so upgrades
    // still feel like progress: a player four times as powerful meets enemies
    // roughly 2.3x as tough, not 4x.
    private const float HealthPowerExponent = 0.6f;
    private const float DamagePowerExponent = 0.35f;
    private const float CountPowerExponent = 0.25f;
    private const float MaxHealthPowerScale = 8f;
    private const float MaxDamagePowerScale = 3f;
    private const float MaxCountPowerScale = 1.75f;

    /// <summary>
    /// Spawns the room's enemy group dormant and returns it. The mix of enemy
    /// types unlocks with ring distance; elite dens promote low-tier enemies.
    /// </summary>
    public static List<EnemyBase> SpawnDormant(
        IReadOnlyList<EnemyBase> prefabs,
        DungeonLayout layout,
        Vector2Int room,
        DungeonLayout.RoomArchetype archetype
    )
    {
        var spawned = new List<EnemyBase>();
        DungeonLayout.RoomPopulation population = layout.Population(room);
        if (population.Count <= 0 || prefabs == null || prefabs.Count == 0)
            return spawned;

        int ring = DungeonLayout.Ring(room);
        var allowed = new List<EnemyBase>();
        foreach (EnemyBase prefab in prefabs)
        {
            if (prefab == null)
                continue;
            int minRing = MinRingFor(prefab.name);
            // Elite dens promote the weakest enemies instead of using strong ones.
            if (population.Elite ? minRing <= 1 : ring >= minRing)
                allowed.Add(prefab);
        }
        if (allowed.Count == 0)
            return spawned;

        EnemyBase swarmSpider = population.IsSpiderSwarm ? FindSpider(allowed) : null;

        System.Random random = layout.RoomRandom(room, 606);
        float power = CurrentPlayerPower();
        float healthScale =
            layout.EnemyHealthScale(room)
            * PowerScale(power, HealthPowerExponent, MaxHealthPowerScale);
        float damageScale = PowerScale(power, DamagePowerExponent, MaxDamagePowerScale);
        Vector2 roomCenter = DungeonLayout.RoomCenter(room);

        int spawnCount = Mathf.Min(population.Count, archetype.EnemyCapacity);
        if (!population.IsSpiderSwarm)
        {
            spawnCount = Mathf.Min(
                archetype.EnemyCapacity,
                Mathf.RoundToInt(
                    spawnCount * PowerScale(power, CountPowerExponent, MaxCountPowerScale)
                )
            );
        }
        for (int i = 0; i < spawnCount; i++)
        {
            EnemyBase prefab = PickEnemy(allowed, swarmSpider, archetype, i, random);
            Vector3 position = PickSpot(roomCenter, archetype, random).ToWorld();

            EnemyBase enemy = PoolManager.Instance?.GetEnemy(prefab, position, Quaternion.identity);
            if (enemy != null)
            {
                enemy.SetPooled(true);
                enemy.ResetForPool();
            }
            else
            {
                enemy = Object.Instantiate(prefab, position, Quaternion.identity);
            }

            enemy.Health *= healthScale;
            enemy.MaxHealth *= healthScale;
            enemy.Damage *= damageScale;

            if (population.Elite)
            {
                enemy.MakeElite();
                enemy.OnEliteDeath -= DropEliteReward;
                enemy.OnEliteDeath += DropEliteReward;
            }

            if (enemy.GetComponent<DungeonEnemyNavigator>() == null)
                enemy.gameObject.AddComponent<DungeonEnemyNavigator>();

            // Visible but dormant: the enemy stands in the room, its AI off.
            enemy.enabled = false;
            spawned.Add(enemy);
        }

        return spawned;
    }

    /// <summary>Wakes a dormant enemy group when its room is entered.</summary>
    public static void Activate(List<EnemyBase> dormant)
    {
        if (dormant == null)
            return;

        foreach (EnemyBase enemy in dormant)
        {
            if (enemy != null)
                enemy.enabled = true;
        }
        dormant.Clear();
    }

    /// <summary>
    /// Moves dormant enemies onto the freshly baked walkable surface. This
    /// prevents a random spawn from beginning inside a prop or just beyond an
    /// interior-wall NavMesh boundary.
    /// </summary>
    public static void AlignToNavMesh(List<EnemyBase> dormant)
    {
        if (dormant == null)
            return;

        foreach (EnemyBase enemy in dormant)
        {
            if (
                enemy == null
                || !NavMesh.SamplePosition(
                    enemy.transform.position,
                    out NavMeshHit hit,
                    4f,
                    NavMesh.AllAreas
                )
            )
                continue;

            Vector3 position = hit.position;
            position.y = enemy.transform.position.y;
            if (enemy.rb != null)
            {
                enemy.rb.position = position;
                enemy.rb.linearVelocity = Vector3.zero;
                enemy.rb.angularVelocity = Vector3.zero;
            }
            else
            {
                enemy.transform.position = position;
            }
        }
    }

    /// <summary>Returns a still-dormant group to the pool when its room unloads.</summary>
    public static void Despawn(List<EnemyBase> dormant)
    {
        if (dormant == null)
            return;

        foreach (EnemyBase enemy in dormant)
        {
            if (enemy == null)
                continue;
            // Re-enable the AI so the pooled instance behaves when reused.
            enemy.enabled = true;
            if (PoolManager.Instance != null)
                PoolManager.Instance.ReturnEnemy(enemy);
            else
                Object.Destroy(enemy.gameObject);
        }
        dormant.Clear();
    }

    private static float CurrentPlayerPower()
    {
        PlayerStats stats = PlayerStats.Resolve();
        return stats != null ? stats.ComputePowerScore() : 1f;
    }

    /// <summary>A power multiplier that never weakens enemies below baseline.</summary>
    private static float PowerScale(float power, float exponent, float max)
    {
        return Mathf.Clamp(Mathf.Pow(Mathf.Max(1f, power), exponent), 1f, max);
    }

    private static void DropEliteReward(Vector3 position)
    {
        GameObject prefab = LootChest.PickWeightedBoost(
            Object.FindAnyObjectByType<BoostHandler>()?.BoostPrefabs
        );
        if (prefab != null)
            Object.Instantiate(prefab, position, Quaternion.identity);
    }

    private static EnemyBase PickEnemy(
        List<EnemyBase> allowed,
        EnemyBase swarmSpider,
        DungeonLayout.RoomArchetype archetype,
        int spawnIndex,
        System.Random random
    )
    {
        if (swarmSpider != null)
            return swarmSpider;

        // Grand arenas should visibly include the restored fast archetype
        // rather than relying on a low-probability uniform roll.
        if (archetype.Shape == DungeonLayout.RoomShape.GrandArena && spawnIndex == 0)
        {
            foreach (EnemyBase candidate in allowed)
            {
                if (candidate.name.Contains("Spider"))
                    return candidate;
            }
        }

        return allowed[random.Next(allowed.Count)];
    }

    private static EnemyBase FindSpider(List<EnemyBase> allowed)
    {
        foreach (EnemyBase candidate in allowed)
        {
            if (candidate.name.Contains("Spider"))
                return candidate;
        }

        return null;
    }

    private static Vector2 PickSpot(
        Vector2 roomCenter,
        DungeonLayout.RoomArchetype archetype,
        System.Random random
    )
    {
        float halfWidth = archetype.HalfWidth;
        float halfDepth = archetype.HalfDepth;
        float centerClearRadius = Mathf.Min(
            CenterClearRadius,
            Mathf.Max(1.25f, Mathf.Min(halfWidth, halfDepth) * 0.55f)
        );

        for (int attempt = 0; attempt < 24; attempt++)
        {
            var offset = new Vector2(
                Mathf.Lerp(-halfWidth, halfWidth, (float)random.NextDouble()),
                Mathf.Lerp(-halfDepth, halfDepth, (float)random.NextDouble())
            );
            // Leave the middle of the room clear so the player never walks
            // straight into a spawn through a doorway.
            if (offset.sqrMagnitude < centerClearRadius * centerClearRadius)
                continue;
            if (IsOnDivider(offset, archetype))
                continue;
            return roomCenter + offset;
        }

        return roomCenter + new Vector2(halfWidth, halfDepth);
    }

    private static bool IsOnDivider(Vector2 offset, DungeonLayout.RoomArchetype archetype)
    {
        if (archetype.Shape != DungeonLayout.RoomShape.Divided)
            return false;

        if ((archetype.Variant & 1) == 0)
        {
            float nearestVerticalSegment = Mathf.Min(
                Mathf.Abs(offset.y),
                Mathf.Abs(Mathf.Abs(offset.y) - 8f)
            );
            return Mathf.Abs(offset.x) < 1.5f && nearestVerticalSegment < 2.6f;
        }

        float nearestSegment = Mathf.Min(
            Mathf.Abs(offset.x - 4f),
            Mathf.Abs(offset.x + 4f),
            Mathf.Abs(Mathf.Abs(offset.x) - 12f)
        );
        return Mathf.Abs(offset.y) < 1.5f && nearestSegment < 2.6f;
    }

    /// <summary>The minimum ring distance at which an enemy type appears.</summary>
    private static int MinRingFor(string prefabName)
    {
        if (prefabName.Contains("Hydra"))
            return 4;
        if (prefabName.Contains("HardChunky") || prefabName.Contains("ShootingHard"))
            return 5;
        if (prefabName.Contains("Hard") || prefabName.Contains("Shooting"))
            return 3;
        if (prefabName.Contains("Normal") || prefabName.Contains("Spider"))
            return 2;
        return 1;
    }
}
