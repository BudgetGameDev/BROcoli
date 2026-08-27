using System.Collections.Generic;
using UnityEngine;

public partial class DungeonPropPlacer
{
    private void BuildVault(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<Vector2> occupied
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
        List<Vector2> occupied
    )
    {
        int count = Mathf.Min(maxPropsPerRoom, 9 + random.Next(0, 7));
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            float x = Mathf.Lerp(-archetype.HalfWidth + 1f, archetype.HalfWidth - 1f, t);
            float z = (archetype.Variant & 1) == 0 ? x * 0.45f : -x * 0.45f;
            z += Mathf.Lerp(-1.4f, 1.4f, (float)random.NextDouble());
            z = Mathf.Clamp(z, -archetype.HalfDepth + 0.8f, archetype.HalfDepth - 0.8f);
            string token =
                i % 3 == 0 ? "Rocks"
                : i % 3 == 1 ? "Stones"
                : "Dirt";
            PlaceNamed(
                parent,
                center,
                token,
                new Vector2(x, z),
                random.Next(0, 360),
                occupied,
                Mathf.Lerp(0.75f, 1.25f, (float)random.NextDouble())
            );
        }
    }

    private void Scatter(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<Vector2> occupied,
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

            float clearance = Clearance(prefab.name);
            if (!TryRandomSpot(archetype, random, occupied, clearance, out Vector2 local))
                continue;
            Instantiate(
                prefab,
                (center + local).ToWorld(),
                GroundPlane.YawRotation(random.Next(0, 360)),
                parent
            );
            occupied.Add(local);
        }
    }

    private void PlaceNamed(
        Transform parent,
        Vector2 center,
        string token,
        Vector2 local,
        float yaw,
        List<Vector2> occupied,
        float scale = 1f,
        float height = 0f
    )
    {
        GameObject prefab = FindProp(token);
        if (prefab == null)
            return;
        GameObject prop = Instantiate(
            prefab,
            (center + local).ToWorld(height),
            Quaternion.Euler(0f, yaw, 0f),
            parent
        );
        prop.transform.localScale *= scale;
        occupied.Add(local);
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

        float wallX = archetype.Shape switch
        {
            DungeonLayout.RoomShape.Compact => 6f,
            DungeonLayout.RoomShape.LongVertical => 6f,
            DungeonLayout.RoomShape.LargeSquare => 10f,
            _ => HalfRoomWidth,
        };
        float wallZ = archetype.Shape switch
        {
            DungeonLayout.RoomShape.Compact => 6f,
            DungeonLayout.RoomShape.LongHorizontal => 6f,
            _ => HalfRoomDepth,
        };
        Vector2 local;
        float yaw;
        switch ((side % 4 + 4) % 4)
        {
            case 0:
                local = new Vector2(-3.5f, wallZ);
                yaw = 0f;
                break;
            case 1:
                local = new Vector2(wallX, -3f);
                yaw = 90f;
                break;
            case 2:
                local = new Vector2(3.5f, -wallZ);
                yaw = 180f;
                break;
            default:
                local = new Vector2(-wallX, 3f);
                yaw = -90f;
                break;
        }
        Instantiate(prefab, (center + local).ToWorld(), Quaternion.Euler(0f, yaw, 0f), parent);
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
