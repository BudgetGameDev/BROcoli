using System.Collections.Generic;
using UnityEngine;

public partial class DungeonPropPlacer
{
    private void BuildVault(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<OccupiedSpot> occupied
    )
    {
        float x = Mathf.Min(4.5f, archetype.HalfWidth - 0.7f);
        float z = Mathf.Min(4.4f, archetype.HalfDepth - 0.7f);
        foreach (
            Vector2 p in new[]
            {
                new Vector2(-x, -z),
                new Vector2(x, -z),
                new Vector2(-x, z),
                new Vector2(x, z),
            }
        )
            PlaceNamed(parent, center, "Column", p, 0f, occupied);

        foreach (
            Vector2 p in new[]
            {
                new Vector2(-1.2f, -1f),
                new Vector2(1.2f, -1f),
                new Vector2(-1.2f, 1f),
                new Vector2(1.2f, 1f),
            }
        )
            PlaceNamed(parent, center, "Coin", p, random.Next(0, 360), occupied);
        PlaceWallBanner(parent, center, archetype, archetype.Variant);
        PlaceWallBanner(parent, center, archetype, archetype.Variant + 2);
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
            5 + random.Next(0, 4),
            "Rocks",
            "Stones"
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

            float radius = FootprintRadius(prefab);
            bool large = IsLargeProp(prefab.name);
            if (!TryRandomSpot(archetype, random, occupied, radius, large, out Vector2 local))
                continue;
            Instantiate(
                prefab,
                (center + local).ToWorld(),
                GroundPlane.YawRotation(random.Next(0, 360)),
                parent
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
            float propRadius = FootprintRadius(prefab);
            float neighbourDistance = propRadius * 2f + TightClusterGap;
            float ringRadius = neighbourDistance / (2f * Mathf.Sin(Mathf.PI / groupSize));
            float clusterRadius = ringRadius + propRadius;
            if (
                !TryClusterSpot(archetype, random, occupied, clusterRadius, out Vector2 clusterSpot)
            )
                continue;

            float phase = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble());
            for (int i = 0; i < groupSize; i++)
            {
                float angle = phase + i * Mathf.PI * 2f / groupSize;
                Vector2 local =
                    clusterSpot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                Instantiate(
                    prefab,
                    (center + local).ToWorld(),
                    GroundPlane.YawRotation(random.Next(0, 360)),
                    parent
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
        float height = 0f
    )
    {
        GameObject prefab = FindProp(token);
        if (prefab == null)
            return;
        float radius = FootprintRadius(prefab) * scale;
        if (OverlapsReservedChest(local, radius, occupied))
            return;
        GameObject prop = Instantiate(
            prefab,
            (center + local).ToWorld(height),
            Quaternion.Euler(0f, yaw, 0f),
            parent
        );
        prop.transform.localScale *= scale;
        occupied.Add(new OccupiedSpot(local, radius, IsLargeProp(prefab.name)));
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

    private void PlaceWallBanner(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        int side
    )
    {
        GameObject prefab = FindProp("Banner");
        if (prefab == null)
            return;

        if (
            !DungeonWallDressing.TryBannerMount(
                archetype,
                doorways,
                side,
                out DungeonWallMount mount
            )
        )
            return;

        Instantiate(
            prefab,
            (center + mount.Local).ToWorld(),
            GroundPlane.YawRotation(mount.Yaw),
            parent
        );
    }

    private GameObject FindProp(string token)
    {
        if (propPrefabs == null)
            return null;
        string normalized = token.Replace("-", string.Empty);
        foreach (GameObject prefab in propPrefabs)
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
