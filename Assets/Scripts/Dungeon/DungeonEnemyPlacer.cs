using System.Collections.Generic;
using Pooling;
using UnityEngine;

/// <summary>
/// Spawns a room's enemies ahead of the player's arrival. Enemies come from
/// the shared <see cref="PoolManager"/> and stand visibly in the room but
/// dormant (their AI component disabled), so there is no pop-in when the
/// player peers through a doorway; entering the room wakes the whole group.
/// </summary>
public static class DungeonEnemyPlacer
{
    private const float InnerMargin = 3.5f;
    private const float CenterClearRadius = 4f;

    /// <summary>
    /// Spawns the room's enemy group dormant and returns it. The mix of enemy
    /// types unlocks with ring distance; elite dens promote low-tier enemies.
    /// </summary>
    public static List<EnemyBase> SpawnDormant(
        IReadOnlyList<EnemyBase> prefabs,
        DungeonLayout layout,
        Vector2Int room
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

        System.Random random = layout.RoomRandom(room, 606);
        float healthScale = layout.EnemyHealthScale(room);
        Vector2 roomCenter = DungeonLayout.RoomCenter(room);

        for (int i = 0; i < population.Count; i++)
        {
            EnemyBase prefab = allowed[random.Next(allowed.Count)];
            Vector3 position = PickSpot(roomCenter, random).ToWorld();

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

    private static void DropEliteReward(Vector3 position)
    {
        GameObject prefab = LootChest.PickWeightedBoost(
            Object.FindAnyObjectByType<BoostHandler>()?.BoostPrefabs
        );
        if (prefab != null)
            Object.Instantiate(prefab, position, Quaternion.identity);
    }

    private static Vector2 PickSpot(Vector2 roomCenter, System.Random random)
    {
        float halfWidth = DungeonLayout.RoomWidth / 2f - InnerMargin;
        float halfDepth = DungeonLayout.RoomDepth / 2f - InnerMargin;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            var offset = new Vector2(
                Mathf.Lerp(-halfWidth, halfWidth, (float)random.NextDouble()),
                Mathf.Lerp(-halfDepth, halfDepth, (float)random.NextDouble())
            );
            // Leave the middle of the room clear so the player never walks
            // straight into a spawn through a doorway.
            if (offset.sqrMagnitude >= CenterClearRadius * CenterClearRadius)
                return roomCenter + offset;
        }

        return roomCenter + new Vector2(halfWidth, halfDepth);
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
