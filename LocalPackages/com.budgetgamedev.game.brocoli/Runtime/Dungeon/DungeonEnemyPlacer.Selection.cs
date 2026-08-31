using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public static partial class DungeonEnemyPlacer
    {
        internal static EnemyBase PickEnemy(
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

        internal static EnemyBase FindSpider(List<EnemyBase> allowed)
        {
            foreach (EnemyBase candidate in allowed)
            {
                if (candidate.name.Contains("Spider"))
                    return candidate;
            }

            return null;
        }

        internal static Vector2 PickSpot(
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

        internal static bool IsOnDivider(Vector2 offset, DungeonLayout.RoomArchetype archetype)
        {
            return DungeonPropPlacer.IsOnDivider(offset, archetype);
        }

        /// <summary>The minimum ring distance at which an enemy type appears.</summary>
        internal static int MinRingFor(string prefabName)
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
}
