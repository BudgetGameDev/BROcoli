using System.Collections.Generic;
using Pooling;
using UnityEngine;

/// <summary>
/// Spawns a room's enemies ahead of the player's arrival. Enemies come from
/// the shared <see cref="PoolManager"/> and are left dormant (inactive) so
/// they neither chase nor take damage until the player actually walks into
/// the room, which then wakes them all at once.
/// </summary>
public static class DungeonEnemyPlacer
{
    private const float InnerMargin = 3.5f;
    private const float CenterClearRadius = 4f;

    /// <summary>
    /// Spawns the room's enemy group dormant and returns it. The mix of enemy
    /// types unlocks with ring distance so early rooms stay gentle.
    /// </summary>
    public static List<EnemyBase> SpawnDormant(
        IReadOnlyList<EnemyBase> prefabs,
        DungeonLayout layout,
        Vector2Int room
    )
    {
        var spawned = new List<EnemyBase>();
        int count = layout.EnemyCount(room);
        if (count <= 0 || prefabs == null || prefabs.Count == 0)
            return spawned;

        int ring = DungeonLayout.Ring(room);
        var allowed = new List<EnemyBase>();
        foreach (EnemyBase prefab in prefabs)
        {
            if (prefab != null && ring >= MinRingFor(prefab.name))
                allowed.Add(prefab);
        }
        if (allowed.Count == 0)
            return spawned;

        System.Random random = layout.RoomRandom(room, 606);
        float healthScale = layout.EnemyHealthScale(room);
        Vector2 roomCenter = DungeonLayout.RoomCenter(room);

        for (int i = 0; i < count; i++)
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
            enemy.gameObject.SetActive(false);
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
            if (enemy != null && !enemy.gameObject.activeSelf)
                enemy.gameObject.SetActive(true);
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
            if (PoolManager.Instance != null)
                PoolManager.Instance.ReturnEnemy(enemy);
            else
                Object.Destroy(enemy.gameObject);
        }
        dormant.Clear();
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
