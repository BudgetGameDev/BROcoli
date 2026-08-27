using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dresses deterministic dungeon archetypes with themed prop patterns, loot,
/// wall lighting, and water. Layouts range from empty rooms to dense storage,
/// banquet, armoury, shrine, collapsed, flooded, and treasure rooms.
/// </summary>
public partial class DungeonPropPlacer : MonoBehaviour
{
    private const float HalfRoomWidth = DungeonLayout.RoomWidth / 2f;
    private const float HalfRoomDepth = DungeonLayout.RoomDepth / 2f;

    // The Kenney wall mesh is asymmetric around its prefab origin. Its upright
    // structural slab occupies local Z 0.4..1.0; the rest of its renderer
    // bounds is floor-level moulding. Horizontal walls use local Z directly,
    // while the builder's +90 degree vertical rotation maps it to world X.
    // Mount wall props on the slab faces, not on the moulding's outer bounds.
    private const float WallFrontFaceOffset = 0.4f;
    private const float WallBackFaceOffset = 1f;
    private const float BannerMeshDepthOffset = 1.05f;
    private const float PropGap = 0.12f;
    private const float LargePropSeparation = 3.8f;

    private readonly struct OccupiedSpot
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly bool Large;

        public OccupiedSpot(Vector2 position, float radius, bool large)
        {
            Position = position;
            Radius = radius;
            Large = large;
        }
    }

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

    private readonly Dictionary<GameObject, float> footprintRadii = new();

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
        var occupied = new List<OccupiedSpot>();

        int chestCount = ChestCount(room, archetype, random);
        for (int slot = 0; slot < chestCount; slot++)
        {
            bool golden =
                goldenChestPrefab != null
                && (
                    archetype.Theme == DungeonLayout.RoomTheme.TreasureVault
                        ? slot == archetype.Variant % chestCount
                        : random.NextDouble() < goldenChestChance
                );
            GameObject prefab = golden ? goldenChestPrefab : chestPrefab;
            Vector2 local = ChestSpot(slot, chestCount, archetype);
            float radius = FootprintRadius(prefab);
            occupied.Add(new OccupiedSpot(local, radius, radius >= 1.1f));

            if (openedChestSlots != null && openedChestSlots.Contains(slot))
                continue;

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
        bool dampClutter =
            archetype.Theme == DungeonLayout.RoomTheme.Sparse
            || archetype.Theme == DungeonLayout.RoomTheme.Collapsed;
        if (!flooded && !dampClutter)
            return;

        float adjustedChance =
            archetype.Theme == DungeonLayout.RoomTheme.Collapsed
                ? Mathf.Min(1f, waterChance + 0.2f)
                : waterChance;
        if (!flooded && random.NextDouble() >= adjustedChance)
            return;

        int poolCount = flooded ? 2 + random.Next(0, 3) : 1 + random.Next(0, 2);
        Vector2 poolingPoint = PoolSpot(archetype, random);
        var placedPools = new List<PoolPlacement>(poolCount);
        for (int i = 0; i < poolCount; i++)
        {
            Vector2 preferred =
                i == 0
                    ? poolingPoint
                    : poolingPoint
                        + new Vector2(
                            Mathf.Lerp(-3.2f, 3.2f, (float)random.NextDouble()),
                            Mathf.Lerp(-2.4f, 2.4f, (float)random.NextDouble())
                        );
            float maxScale = archetype.Shape == DungeonLayout.RoomShape.Compact ? 0.84f : 1.5f;
            float scale = Mathf.Lerp(0.62f, maxScale, (float)random.NextDouble());
            float yaw = random.Next(0, 360);
            if (
                !TryPoolSpot(
                    archetype,
                    random,
                    placedPools,
                    preferred,
                    scale,
                    yaw,
                    out PoolPlacement placement
                )
            )
                continue;

            GameObject pool = Instantiate(
                waterPrefab,
                (center + placement.Center).ToWorld(0.02f + placedPools.Count * 0.007f),
                GroundPlane.YawRotation(yaw),
                parent
            );
            pool.transform.localScale *= scale;
            placedPools.Add(placement);
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
        if (
            archetype.Theme == DungeonLayout.RoomTheme.Storage
            || archetype.Theme == DungeonLayout.RoomTheme.Shrine
        )
            chance += 0.12f;
        return random.NextDouble() < chance ? 1 : 0;
    }

    private static Vector2 ChestSpot(int slot, int count, DungeonLayout.RoomArchetype archetype)
    {
        if (count == 1)
        {
            float x = archetype.HalfWidth - 1.4f;
            float z = archetype.HalfDepth - 1.4f;
            float xSign = (archetype.Variant & 1) == 0 ? 1f : -1f;
            if (
                archetype.Shape == DungeonLayout.RoomShape.Compact
                && archetype.Theme == DungeonLayout.RoomTheme.Shrine
            )
            {
                return new Vector2(xSign * 2f, xSign * -2f);
            }
            if (archetype.Theme == DungeonLayout.RoomTheme.Armory)
            {
                float armoryZ = Mathf.Min(2f, z);
                return new Vector2(xSign * x, xSign * -armoryZ);
            }
            return new Vector2(xSign * x, (archetype.Variant & 2) == 0 ? z : -z);
        }

        float vaultX = archetype.Shape == DungeonLayout.RoomShape.Compact ? 2.3f : 3.3f;
        float vaultZ = archetype.Shape == DungeonLayout.RoomShape.Compact ? 2.3f : 3f;
        var vaultSpots = new[]
        {
            new Vector2(-vaultX, -vaultZ),
            new Vector2(vaultX, -vaultZ),
            new Vector2(-vaultX, vaultZ),
            new Vector2(vaultX, vaultZ),
        };
        return RotateQuarterTurns(vaultSpots[slot % vaultSpots.Length], archetype.Variant);
    }
}
