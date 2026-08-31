using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        private static readonly HashSet<string> ReportedMissingTokens = new();

        private void BuildVault(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            foreach (
                Vector2 p in new[]
                {
                    new Vector2(-1.2f, -1f),
                    new Vector2(1.2f, -1f),
                    new Vector2(-1.2f, 1f),
                    new Vector2(1.2f, 1f),
                }
            )
                PlaceNamed(
                    parent,
                    center,
                    DungeonPropTokens.Coin,
                    p,
                    random.Next(0, 360),
                    occupied
                );

            Scatter(
                parent,
                center,
                archetype,
                random,
                occupied,
                4 + random.Next(0, 4),
                DungeonPropTokens.Coin,
                DungeonPropTokens.Pot,
                DungeonPropTokens.Key
            );
            PlaceSmallClusters(
                parent,
                center,
                archetype,
                random,
                occupied,
                2,
                3,
                6,
                DungeonPropTokens.Coin,
                DungeonPropTokens.Pot,
                DungeonPropTokens.Barrel
            );
        }

        private void BuildCollapsed(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            Scatter(
                parent,
                center,
                archetype,
                random,
                occupied,
                8 + random.Next(0, 5),
                DungeonPropTokens.Rocks,
                DungeonPropTokens.Stones
            );
            PlaceSmallClusters(
                parent,
                center,
                archetype,
                random,
                occupied,
                1 + random.Next(0, 2),
                3,
                6,
                DungeonPropTokens.Stones,
                DungeonPropTokens.Pot
            );
        }

        private void Scatter(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied,
            int requested,
            params string[] tokens
        )
        {
            int count = Mathf.Min(maxPropsPerRoom, requested);
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = FindProp(tokens[random.Next(tokens.Length)]);
                if (prefab == null)
                    continue;

                DungeonPropMeasurement measurement = Measure(prefab);
                float radius = measurement.Radius;
                bool large = measurement.IsLarge;
                if (!TryRandomSpot(archetype, random, occupied, radius, large, out Vector2 local))
                    continue;
                SpawnProp(
                    parent,
                    prefab,
                    center + local,
                    GroundPlane.YawRotation(random.Next(0, 360))
                );
                occupied.Add(new OccupiedSpot(local, radius, large));
            }
        }

        private void PlaceSmallClusters(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied,
            int clusterCount,
            int minGroupSize,
            int maxGroupSize,
            params string[] tokens
        )
        {
            if (tokens == null || tokens.Length == 0)
                return;

            int minimum = Mathf.Max(3, minGroupSize);
            int maximum = Mathf.Max(minimum, maxGroupSize);
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                GameObject prefab = FindProp(tokens[random.Next(tokens.Length)]);
                if (prefab == null)
                    continue;

                int groupSize = random.Next(minimum, maximum + 1);
                float propRadius = Measure(prefab).Radius;
                float neighbourDistance = propRadius * 2f + TightClusterGap;
                float ringRadius = neighbourDistance / (2f * Mathf.Sin(Mathf.PI / groupSize));
                float clusterRadius = ringRadius + propRadius;
                if (
                    !TryClusterSpot(
                        archetype,
                        random,
                        occupied,
                        clusterRadius,
                        out Vector2 clusterSpot
                    )
                )
                    continue;

                float phase = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble());
                for (int i = 0; i < groupSize; i++)
                {
                    float angle = phase + i * Mathf.PI * 2f / groupSize;
                    Vector2 local =
                        clusterSpot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                    SpawnProp(
                        parent,
                        prefab,
                        center + local,
                        GroundPlane.YawRotation(random.Next(0, 360))
                    );
                    occupied.Add(new OccupiedSpot(local, propRadius, false));
                }
            }
        }

        private void PlaceNamed(
            Transform parent,
            Vector2 center,
            string token,
            Vector2 local,
            float yaw,
            List<OccupiedSpot> occupied,
            float scale = 1f,
            float lift = 0f
        )
        {
            GameObject prefab = FindProp(token);
            if (prefab == null)
                return;

            DungeonPropMeasurement measurement = Measure(prefab);
            float radius = measurement.Radius * scale;
            if (OverlapsReservedChest(local, radius, occupied))
                return;
            SpawnProp(parent, prefab, center + local, Quaternion.Euler(0f, yaw, 0f), scale, lift);
            occupied.Add(new OccupiedSpot(local, radius, measurement.IsLarge));
        }

        private static bool OverlapsReservedChest(
            Vector2 local,
            float radius,
            List<OccupiedSpot> occupied
        )
        {
            foreach (OccupiedSpot spot in occupied)
            {
                if (!spot.ReservedForChest)
                    continue;
                float separation = radius + spot.Radius + PropGap;
                if ((local - spot.Position).sqrMagnitude < separation * separation)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The registered prop a theme is asking for.
        ///
        /// A theme names the props it wants because no measurement can say that a
        /// chair belongs beside a table. What measurement cannot do, saying so can:
        /// a token that resolves to nothing used to place nothing and report
        /// nothing, which is how shrines and vaults lost their pillars unnoticed.
        /// DungeonPropCatalogTests turns that into a failing gate; this keeps a
        /// running game honest about it too.
        /// </summary>
        private GameObject FindProp(string token)
        {
            GameObject prefab = ResolveProp(propPrefabs, token);
            if (prefab == null && ReportedMissingTokens.Add(token))
            {
                Debug.LogWarning(
                    $"DungeonPropPlacer: no prop prefab matches \"{token}\". Rooms asking for it "
                        + "are being built without it. Register a matching prefab in propPrefabs "
                        + "or stop asking for the token."
                );
            }
            return prefab;
        }

        /// <summary>
        /// How a token is matched against a prop set, with no state and no side
        /// effects, so the tests can ask the same question of the same rule.
        /// </summary>
        public static GameObject ResolveProp(
            System.Collections.Generic.IReadOnlyList<GameObject> prefabs,
            string token
        )
        {
            if (prefabs == null || string.IsNullOrEmpty(token))
                return null;
            string normalized = token.Replace("-", string.Empty);
            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null)
                    continue;
                string name = prefab.name.Replace("-", string.Empty);
                if (name.IndexOf(normalized, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return prefab;
            }
            return null;
        }
    }
}
