using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dresses deterministic dungeon archetypes with themed prop patterns, loot,
/// wall lighting, and water. Layouts range from empty rooms to dense storage,
/// banquet, armoury, shrine, collapsed, flooded, and treasure rooms.
/// </summary>
public class DungeonPropPlacer : MonoBehaviour
{
    private const float HalfRoomWidth = DungeonLayout.RoomWidth / 2f;
    private const float HalfRoomDepth = DungeonLayout.RoomDepth / 2f;
    private const float TorchWallInset = 1f;

    public readonly struct PlacedChest
    {
        public readonly LootChest Chest;
        public readonly int Slot;

        public PlacedChest(LootChest chest, int slot)
        {
            Chest = chest;
            Slot = slot;
        }
    }

    [Header("Loot")]
    [SerializeField]
    private GameObject chestPrefab;

    [SerializeField]
    private GameObject goldenChestPrefab;

    [SerializeField, Range(0f, 1f)]
    private float chestChance = 0.35f;

    [SerializeField, Range(0f, 1f)]
    private float goldenChestChance = 0.12f;

    [Header("Props")]
    [SerializeField]
    private GameObject[] propPrefabs;

    [SerializeField, Min(1)]
    private int maxPropsPerRoom = 18;

    [Header("Atmosphere")]
    [SerializeField]
    private GameObject torchPrefab;

    [SerializeField]
    private GameObject waterPrefab;

    [SerializeField, Range(0f, 1f)]
    private float waterChance = 0.45f;

    /// <summary>
    /// Places deterministic chest slots and the room's prop pattern. Opened
    /// slots still reserve their positions so rebuilt rooms never rearrange.
    /// </summary>
    public List<PlacedChest> BuildContents(
        Transform parent,
        Vector2Int room,
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        ISet<int> openedChestSlots
    )
    {
        Vector2 center = DungeonLayout.RoomCenter(room);
        var placedChests = new List<PlacedChest>();
        var occupied = new List<Vector2>();

        int chestCount = ChestCount(room, archetype, random);
        for (int slot = 0; slot < chestCount; slot++)
        {
            bool golden = goldenChestPrefab != null
                && (archetype.Theme == DungeonLayout.RoomTheme.TreasureVault
                    ? slot == archetype.Variant % chestCount
                    : random.NextDouble() < goldenChestChance);
            Vector2 local = ChestSpot(slot, chestCount, archetype);
            occupied.Add(local);

            if (openedChestSlots != null && openedChestSlots.Contains(slot))
                continue;

            GameObject prefab = golden ? goldenChestPrefab : chestPrefab;
            if (prefab == null)
                continue;
            GameObject spawned = Instantiate(
                prefab,
                (center + local).ToWorld(),
                GroundPlane.YawRotation((archetype.Variant * 90 + slot * 90) % 360),
                parent
            );
            LootChest chest = spawned.GetComponent<LootChest>();
            if (chest != null)
                placedChests.Add(new PlacedChest(chest, slot));
        }

        BuildThemeProps(parent, center, archetype, random, occupied);
        return placedChests;
    }

    /// <summary>Places lighting and pools appropriate to the room theme.</summary>
    public void BuildAtmosphere(
        Transform parent,
        Vector2Int room,
        DungeonLayout.RoomArchetype archetype,
        System.Random random
    )
    {
        Vector2 center = DungeonLayout.RoomCenter(room);
        if (torchPrefab != null)
        {
            (Vector2 pos, float yaw)[] spots = TorchSpots(archetype);
            Shuffle(spots, random);
            int torchCount = archetype.Theme switch
            {
                DungeonLayout.RoomTheme.Empty => 2,
                DungeonLayout.RoomTheme.Shrine => 4,
                DungeonLayout.RoomTheme.TreasureVault => 4,
                DungeonLayout.RoomTheme.Storage => 3 + random.Next(0, 2),
                DungeonLayout.RoomTheme.Banquet => 3 + random.Next(0, 2),
                _ => 2 + random.Next(0, 3),
            };
            for (int i = 0; i < torchCount && i < spots.Length; i++)
            {
                Instantiate(
                    torchPrefab,
                    (center + spots[i].pos).ToWorld(),
                    Quaternion.Euler(0f, spots[i].yaw, 0f),
                    parent
                );
            }
        }

        if (waterPrefab == null)
            return;

        bool flooded = archetype.Theme == DungeonLayout.RoomTheme.Flooded;
        bool dampClutter = archetype.Theme == DungeonLayout.RoomTheme.Sparse
            || archetype.Theme == DungeonLayout.RoomTheme.Collapsed;
        if (!flooded && !dampClutter)
            return;

        float adjustedChance = archetype.Theme == DungeonLayout.RoomTheme.Collapsed
            ? Mathf.Min(1f, waterChance + 0.2f)
            : waterChance;
        if (!flooded && random.NextDouble() >= adjustedChance)
            return;

        int poolCount = flooded ? 2 + random.Next(0, 3) : 1 + random.Next(0, 2);
        Vector2 poolingPoint = PoolSpot(archetype, random);
        for (int i = 0; i < poolCount; i++)
        {
            Vector2 local = i == 0
                ? poolingPoint
                : ClampToRoom(
                    poolingPoint + new Vector2(
                        Mathf.Lerp(-3.2f, 3.2f, (float)random.NextDouble()),
                        Mathf.Lerp(-2.4f, 2.4f, (float)random.NextDouble())
                    ),
                    archetype,
                    2f
                );
            GameObject pool = Instantiate(
                waterPrefab,
                (center + local).ToWorld(0.02f + i * 0.007f),
                GroundPlane.YawRotation(random.Next(0, 360)),
                parent
            );
            float maxScale = archetype.Shape == DungeonLayout.RoomShape.Compact ? 0.84f : 1.5f;
            float scale = Mathf.Lerp(0.62f, maxScale, (float)random.NextDouble());
            pool.transform.localScale *= scale;
        }
    }

    private int ChestCount(
        Vector2Int room,
        DungeonLayout.RoomArchetype archetype,
        System.Random random
    )
    {
        if (DungeonLayout.Ring(room) == 0 || archetype.Theme == DungeonLayout.RoomTheme.Empty)
            return 0;
        if (archetype.Theme == DungeonLayout.RoomTheme.TreasureVault)
            return 4;

        float chance = chestChance;
        if (archetype.Theme == DungeonLayout.RoomTheme.Storage
            || archetype.Theme == DungeonLayout.RoomTheme.Shrine)
            chance += 0.12f;
        return random.NextDouble() < chance ? 1 : 0;
    }

    private static Vector2 ChestSpot(
        int slot,
        int count,
        DungeonLayout.RoomArchetype archetype
    )
    {
        if (count == 1)
        {
            float x = archetype.HalfWidth - 1.4f;
            float z = archetype.HalfDepth - 1.4f;
            return new Vector2(
                (archetype.Variant & 1) == 0 ? x : -x,
                (archetype.Variant & 2) == 0 ? z : -z
            );
        }

        var vaultSpots = new[]
        {
            new Vector2(-3.3f, -3f), new Vector2(3.3f, -3f),
            new Vector2(-3.3f, 3f), new Vector2(3.3f, 3f),
        };
        return RotateQuarterTurns(vaultSpots[slot % vaultSpots.Length], archetype.Variant);
    }

    private void BuildThemeProps(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<Vector2> occupied
    )
    {
        switch (archetype.Theme)
        {
            case DungeonLayout.RoomTheme.Empty:
                return;
            case DungeonLayout.RoomTheme.Sparse:
                Scatter(parent, center, archetype, random, occupied, random.Next(0, 4),
                    "Barrel", "Pot", "Chair", "Stones");
                break;
            case DungeonLayout.RoomTheme.Storage:
                Scatter(parent, center, archetype, random, occupied, 9 + random.Next(0, 6),
                    "Barrel", "Pot", "WoodSupport", "WoodStructure", "Table");
                PlaceWallBanner(parent, center, archetype, archetype.Variant);
                break;
            case DungeonLayout.RoomTheme.Banquet:
                BuildBanquet(parent, center, archetype, random, occupied);
                break;
            case DungeonLayout.RoomTheme.Armory:
                BuildArmory(parent, center, archetype, random, occupied);
                break;
            case DungeonLayout.RoomTheme.Shrine:
                BuildShrine(parent, center, archetype, occupied);
                break;
            case DungeonLayout.RoomTheme.Flooded:
                Scatter(parent, center, archetype, random, occupied, 3 + random.Next(0, 4),
                    "Rocks", "Stones", "Dirt");
                break;
            case DungeonLayout.RoomTheme.TreasureVault:
                BuildVault(parent, center, archetype, random, occupied);
                break;
            case DungeonLayout.RoomTheme.Collapsed:
                BuildCollapsed(parent, center, archetype, random, occupied);
                break;
        }
    }

    private void BuildBanquet(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<Vector2> occupied
    )
    {
        bool horizontal = archetype.Shape == DungeonLayout.RoomShape.LongHorizontal;
        float[] stations = horizontal ? new[] { -6f, 0f, 6f } : new[] { -3.4f, 3.4f };
        foreach (float station in stations)
        {
            Vector2 table = horizontal ? new Vector2(station, 0f) : new Vector2(0f, station);
            PlaceNamed(parent, center, "Table", table, horizontal ? 0f : 90f, occupied);
            Vector2 side = horizontal ? Vector2.up * 1.55f : Vector2.right * 1.55f;
            PlaceNamed(parent, center, "Chair", table + side, horizontal ? 180f : -90f, occupied);
            PlaceNamed(parent, center, "Chair", table - side, horizontal ? 0f : 90f, occupied);
        }
        PlaceWallBanner(parent, center, archetype, archetype.Variant);
        PlaceWallBanner(parent, center, archetype, archetype.Variant + 2);
        Scatter(parent, center, archetype, random, occupied, 2 + random.Next(0, 3),
            "Pot", "Potion", "Barrel");
    }

    private void BuildArmory(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<Vector2> occupied
    )
    {
        float w = Mathf.Max(3.3f, archetype.HalfWidth - 1.2f);
        float d = Mathf.Max(3.2f, archetype.HalfDepth - 1.1f);
        string[] display = { "ShieldRound", "WeaponSword", "ShieldRectangle", "WeaponSpear" };
        for (int i = 0; i < display.Length; i++)
        {
            float x = Mathf.Lerp(-w, w, (i + 0.5f) / display.Length);
            PlaceNamed(parent, center, display[i], new Vector2(x, d), 0f, occupied);
            PlaceNamed(parent, center, display[(i + 2) % display.Length], new Vector2(x, -d), 180f, occupied);
        }
        PlaceNamed(parent, center, "Trap", new Vector2(-2.8f, -2.4f), 45f, occupied);
        PlaceNamed(parent, center, "Trap", new Vector2(2.8f, 2.4f), 45f, occupied);
        Scatter(parent, center, archetype, random, occupied, 2 + random.Next(0, 4),
            "WoodSupport", "Barrel", "Stones");
    }

    private void BuildShrine(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        List<Vector2> occupied
    )
    {
        float x = Mathf.Min(3.6f, archetype.HalfWidth - 0.9f);
        float z = Mathf.Min(3.6f, archetype.HalfDepth - 0.9f);
        foreach (Vector2 p in new[]
        {
            new Vector2(-x, -z), new Vector2(x, -z),
            new Vector2(-x, z), new Vector2(x, z),
        })
            PlaceNamed(parent, center, "Column", p, 0f, occupied);

        PlaceNamed(parent, center, "Stairs", Vector2.zero, archetype.Variant * 90f, occupied, 1.2f);
        PlaceNamed(parent, center, "Potion", new Vector2(0f, 0.35f), 0f, occupied, 1f, 2.05f);
        PlaceNamed(parent, center, "Coin", new Vector2(-0.75f, 0.2f), 90f, occupied, 1f, 2.05f);
        PlaceNamed(parent, center, "Key", new Vector2(0.75f, 0.2f), -90f, occupied, 1f, 2.05f);
        PlaceWallBanner(parent, center, archetype, archetype.Variant);
    }

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
        foreach (Vector2 p in new[]
        {
            new Vector2(-x, -z), new Vector2(x, -z),
            new Vector2(-x, z), new Vector2(x, z),
        })
            PlaceNamed(parent, center, "Column", p, 0f, occupied);

        foreach (Vector2 p in new[]
        {
            new Vector2(-1.2f, -1f), new Vector2(1.2f, -1f),
            new Vector2(-1.2f, 1f), new Vector2(1.2f, 1f),
        })
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
            string token = i % 3 == 0 ? "Rocks" : i % 3 == 1 ? "Stones" : "Dirt";
            PlaceNamed(parent, center, token, new Vector2(x, z), random.Next(0, 360), occupied,
                Mathf.Lerp(0.75f, 1.25f, (float)random.NextDouble()));
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
            Instantiate(prefab, (center + local).ToWorld(),
                GroundPlane.YawRotation(random.Next(0, 360)), parent);
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
        GameObject prop = Instantiate(prefab, (center + local).ToWorld(height),
            Quaternion.Euler(0f, yaw, 0f), parent);
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
            case 0: local = new Vector2(-3.5f, wallZ); yaw = 0f; break;
            case 1: local = new Vector2(wallX, -3f); yaw = 90f; break;
            case 2: local = new Vector2(3.5f, -wallZ); yaw = 180f; break;
            default: local = new Vector2(-wallX, 3f); yaw = -90f; break;
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

    private static bool TryRandomSpot(
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<Vector2> occupied,
        float clearance,
        out Vector2 result
    )
    {
        for (int attempt = 0; attempt < 28; attempt++)
        {
            var candidate = new Vector2(
                Mathf.Lerp(-archetype.HalfWidth, archetype.HalfWidth, (float)random.NextDouble()),
                Mathf.Lerp(-archetype.HalfDepth, archetype.HalfDepth, (float)random.NextDouble())
            );
            if (Mathf.Abs(candidate.x) < 1.55f || Mathf.Abs(candidate.y) < 1.55f)
                continue;
            if (IsOnDivider(candidate, archetype))
                continue;
            bool clear = true;
            foreach (Vector2 other in occupied)
                clear &= (candidate - other).sqrMagnitude >= clearance * clearance;
            if (!clear)
                continue;
            result = candidate;
            return true;
        }
        result = default;
        return false;
    }

    private static bool IsOnDivider(
        Vector2 point,
        DungeonLayout.RoomArchetype archetype
    )
    {
        if (archetype.Shape != DungeonLayout.RoomShape.Divided)
            return false;
        if ((archetype.Variant & 1) == 0)
        {
            float nearest = Mathf.Min(
                Mathf.Abs(point.y),
                Mathf.Abs(Mathf.Abs(point.y) - 8f)
            );
            return Mathf.Abs(point.x) < 1.5f && nearest < 2.6f;
        }

        float nearestHorizontal = Mathf.Min(
            Mathf.Abs(point.x - 4f),
            Mathf.Abs(point.x + 4f),
            Mathf.Abs(Mathf.Abs(point.x) - 12f)
        );
        return Mathf.Abs(point.y) < 1.5f && nearestHorizontal < 2.6f;
    }

    private static float Clearance(string prefabName)
    {
        if (prefabName.Contains("Rocks") || prefabName.Contains("Structure"))
            return 2.6f;
        if (prefabName.Contains("Table") || prefabName.Contains("Stairs"))
            return 2.2f;
        return 1.45f;
    }

    private static (Vector2 pos, float yaw)[] TorchSpots(DungeonLayout.RoomArchetype archetype)
    {
        if (archetype.Shape == DungeonLayout.RoomShape.Compact)
            return new[]
            {
                (new Vector2(-3.5f, 5f), 180f), (new Vector2(3.5f, 5f), 180f),
                (new Vector2(-3.5f, -5f), 0f), (new Vector2(3.5f, -5f), 0f),
                (new Vector2(5f, -3.5f), -90f), (new Vector2(5f, 3.5f), -90f),
                (new Vector2(-5f, -3.5f), 90f), (new Vector2(-5f, 3.5f), 90f),
            };
        if (archetype.Shape == DungeonLayout.RoomShape.LongHorizontal)
            return new[]
            {
                (new Vector2(-8f, 5f), 180f), (new Vector2(8f, 5f), 180f),
                (new Vector2(-8f, -5f), 0f), (new Vector2(8f, -5f), 0f),
            };
        if (archetype.Shape == DungeonLayout.RoomShape.LongVertical)
            return new[]
            {
                (new Vector2(5f, -4f), -90f), (new Vector2(5f, 4f), -90f),
                (new Vector2(-5f, -4f), 90f), (new Vector2(-5f, 4f), 90f),
            };
        if (archetype.Shape == DungeonLayout.RoomShape.LargeSquare)
            return new[]
            {
                (new Vector2(-6f, HalfRoomDepth - TorchWallInset), 180f),
                (new Vector2(6f, HalfRoomDepth - TorchWallInset), 180f),
                (new Vector2(-6f, -HalfRoomDepth + TorchWallInset), 0f),
                (new Vector2(6f, -HalfRoomDepth + TorchWallInset), 0f),
                (new Vector2(9f, -4f), -90f), (new Vector2(9f, 4f), -90f),
                (new Vector2(-9f, -4f), 90f), (new Vector2(-9f, 4f), 90f),
            };
        return new[]
        {
            (new Vector2(-8f, HalfRoomDepth - TorchWallInset), 180f),
            (new Vector2(8f, HalfRoomDepth - TorchWallInset), 180f),
            (new Vector2(-8f, -HalfRoomDepth + TorchWallInset), 0f),
            (new Vector2(8f, -HalfRoomDepth + TorchWallInset), 0f),
            (new Vector2(HalfRoomWidth - TorchWallInset, -5f), -90f),
            (new Vector2(HalfRoomWidth - TorchWallInset, 5f), -90f),
            (new Vector2(-HalfRoomWidth + TorchWallInset, -5f), 90f),
            (new Vector2(-HalfRoomWidth + TorchWallInset, 5f), 90f),
        };
    }

    private static Vector2 PoolSpot(DungeonLayout.RoomArchetype archetype, System.Random random)
    {
        Vector2 corner = new Vector2(
            (archetype.Variant & 1) == 0 ? -1f : 1f,
            (archetype.Variant & 2) == 0 ? -1f : 1f
        );
        float x = Mathf.Lerp(archetype.HalfWidth * 0.2f, archetype.HalfWidth * 0.62f,
            (float)random.NextDouble());
        float z = Mathf.Lerp(archetype.HalfDepth * 0.2f, archetype.HalfDepth * 0.58f,
            (float)random.NextDouble());
        return new Vector2(x * corner.x, z * corner.y);
    }

    private static Vector2 ClampToRoom(
        Vector2 local,
        DungeonLayout.RoomArchetype archetype,
        float margin
    )
    {
        return new Vector2(
            Mathf.Clamp(local.x, -archetype.HalfWidth + margin, archetype.HalfWidth - margin),
            Mathf.Clamp(local.y, -archetype.HalfDepth + margin, archetype.HalfDepth - margin)
        );
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
