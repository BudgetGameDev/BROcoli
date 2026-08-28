using System.Collections.Generic;
using UnityEngine;

public partial class DungeonPropPlacer
{
    private static bool TryRandomSpot(
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<OccupiedSpot> occupied,
        float radius,
        bool large,
        out Vector2 result
    )
    {
        float edgeMargin = Mathf.Max(0.65f, radius + WallSealGap);
        for (int attempt = 0; attempt < 28; attempt++)
        {
            var candidate = new Vector2(
                Mathf.Lerp(
                    -archetype.HalfWidth + edgeMargin,
                    archetype.HalfWidth - edgeMargin,
                    (float)random.NextDouble()
                ),
                Mathf.Lerp(
                    -archetype.HalfDepth + edgeMargin,
                    archetype.HalfDepth - edgeMargin,
                    (float)random.NextDouble()
                )
            );
            if (Mathf.Abs(candidate.x) < 1.55f || Mathf.Abs(candidate.y) < 1.55f)
                continue;
            if (OverlapsInteriorWall(candidate, radius, archetype))
                continue;
            bool clear = true;
            foreach (OccupiedSpot other in occupied)
            {
                float separation = radius + other.Radius + PropGap;
                if (large && other.Large)
                    separation = Mathf.Max(separation, LargePropSeparation);
                clear &= (candidate - other.Position).sqrMagnitude >= separation * separation;
            }
            if (!clear)
                continue;
            result = candidate;
            return true;
        }
        result = default;
        return false;
    }

    private static bool TryClusterSpot(
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<OccupiedSpot> occupied,
        float radius,
        out Vector2 result
    )
    {
        for (int attempt = 0; attempt < 48; attempt++)
        {
            var candidate = new Vector2(
                Mathf.Lerp(
                    -archetype.HalfWidth + radius + WallSealGap,
                    archetype.HalfWidth - radius - WallSealGap,
                    (float)random.NextDouble()
                ),
                Mathf.Lerp(
                    -archetype.HalfDepth + radius + WallSealGap,
                    archetype.HalfDepth - radius - WallSealGap,
                    (float)random.NextDouble()
                )
            );
            if (Mathf.Abs(candidate.x) < 1.55f)
                continue;
            if (Mathf.Abs(candidate.y) < 1.55f)
                continue;
            if (OverlapsInteriorWall(candidate, radius, archetype))
                continue;

            bool clear = true;
            foreach (OccupiedSpot other in occupied)
            {
                float separation = radius + other.Radius + PropGap;
                clear &= (candidate - other.Position).sqrMagnitude >= separation * separation;
            }
            if (!clear)
                continue;
            result = candidate;
            return true;
        }
        result = default;
        return false;
    }

    private static bool IsOnDivider(Vector2 point, DungeonLayout.RoomArchetype archetype)
    {
        if (archetype.Shape != DungeonLayout.RoomShape.Divided)
            return false;
        if ((archetype.Variant & 1) == 0)
        {
            float nearest = Mathf.Min(Mathf.Abs(point.y - 4f), Mathf.Abs(point.y + 4f));
            return Mathf.Abs(point.x) < 1.5f && nearest < 2.6f;
        }

        float nearestHorizontal = Mathf.Min(
            Mathf.Abs(point.x),
            Mathf.Abs(point.x - 8f),
            Mathf.Abs(point.x + 8f)
        );
        return Mathf.Abs(point.y) < 1.5f && nearestHorizontal < 2.6f;
    }

    private float FootprintRadius(GameObject prefab)
    {
        if (prefab == null)
            return 0.5f;
        if (footprintRadii.TryGetValue(prefab, out float cached))
            return cached;

        float radiusSquared = 0.25f;
        Transform root = prefab.transform;
        foreach (MeshFilter meshFilter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null)
                continue;

            Matrix4x4 toRoot = root.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
            radiusSquared = IncludeFootprintBounds(
                radiusSquared,
                meshFilter.sharedMesh.bounds,
                toRoot
            );
        }
        foreach (BoxCollider collider in prefab.GetComponentsInChildren<BoxCollider>(true))
        {
            if (collider.isTrigger)
                continue;
            Matrix4x4 toRoot = root.worldToLocalMatrix * collider.transform.localToWorldMatrix;
            radiusSquared = IncludeFootprintBounds(
                radiusSquared,
                new Bounds(collider.center, collider.size),
                toRoot
            );
        }

        float radius = Mathf.Sqrt(radiusSquared);
        footprintRadii[prefab] = radius;
        return radius;
    }

    private static float IncludeFootprintBounds(
        float radiusSquared,
        Bounds bounds,
        Matrix4x4 toRoot
    )
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
        {
            Vector3 point = toRoot.MultiplyPoint3x4(
                new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z)
            );
            radiusSquared = Mathf.Max(radiusSquared, point.x * point.x + point.z * point.z);
        }
        return radiusSquared;
    }

    private static bool IsLargeProp(string prefabName)
    {
        return prefabName.Contains("Rocks")
            || prefabName.Contains("Stones")
            || prefabName.Contains("Structure")
            || prefabName.Contains("Support")
            || prefabName.Contains("Table");
    }

    private static float PositiveWallFace(float wallCoordinate)
    {
        return wallCoordinate + WallFrontFaceOffset;
    }

    private static float NegativeWallFace(float wallCoordinate)
    {
        return wallCoordinate + WallBackFaceOffset;
    }

    private static Vector2 PoolSpot(DungeonLayout.RoomArchetype archetype, System.Random random)
    {
        Vector2 corner = new Vector2(
            (archetype.Variant & 1) == 0 ? -1f : 1f,
            (archetype.Variant & 2) == 0 ? -1f : 1f
        );
        float x = Mathf.Lerp(
            archetype.HalfWidth * 0.2f,
            archetype.HalfWidth * 0.62f,
            (float)random.NextDouble()
        );
        float z = Mathf.Lerp(
            archetype.HalfDepth * 0.2f,
            archetype.HalfDepth * 0.58f,
            (float)random.NextDouble()
        );
        return new Vector2(x * corner.x, z * corner.y);
    }

    private static Vector2 RotateQuarterTurns(Vector2 point, int turns)
    {
        return ((turns % 4 + 4) % 4) switch
        {
            1 => new Vector2(point.y, -point.x),
            2 => -point,
            3 => new Vector2(-point.y, point.x),
            _ => point,
        };
    }

    private static void Shuffle<T>(T[] values, System.Random random)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
