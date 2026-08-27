using UnityEngine;

/// <summary>
/// Instantiates the physical pieces of a dungeon room: floor tiles, shared
/// wall runs with open or blocked gateways, decorative props, and loot
/// chests. Pure construction; the layout decisions come from
/// <see cref="DungeonLayout"/> and the per-room random streams handed in by
/// <see cref="DungeonManager"/>.
/// </summary>
public partial class DungeonRoomBuilder : MonoBehaviour
{
    private const float Tile = DungeonLayout.TileSize;
    private const float HalfRoomWidth = DungeonLayout.RoomWidth / 2f;
    private const float HalfRoomDepth = DungeonLayout.RoomDepth / 2f;

    // The wall prefab's upright slab is centred at local Z 0.7 rather than at
    // its root. Shift the complete gate assembly to the same depth so its
    // arch, bars, and colliders stay aligned with the adjoining wall pieces.
    private const float WallSlabCenterOffset = 0.7f;

    [Header("Modular Dungeon Kit pieces")]
    [SerializeField]
    private GameObject floorPrefab;

    [SerializeField]
    private GameObject[] floorVariantPrefabs;

    [SerializeField]
    private GameObject wallPrefab;

    [SerializeField]
    private GameObject gateOpenPrefab;

    [SerializeField]
    private GameObject gateBlockedPrefab;

    [SerializeField, Range(0f, 1f)]
    private float floorVariantChance = 0.18f;

    /// <summary>Builds the 7x5 floor with a theme-specific tile pattern.</summary>
    public void BuildFloor(
        Transform parent,
        Vector2Int room,
        DungeonLayout.RoomArchetype archetype,
        System.Random random
    )
    {
        Vector2 center = DungeonLayout.RoomCenter(room);
        for (int i = 0; i < DungeonLayout.RoomTilesX; i++)
        {
            for (int j = 0; j < DungeonLayout.RoomTilesZ; j++)
            {
                Vector2 tileCenter = TileCenter(center, i, j);
                GameObject prefab = floorPrefab;
                if (
                    floorVariantPrefabs != null
                    && floorVariantPrefabs.Length > 0
                    && UsesDetailedFloor(i, j, archetype, random)
                )
                {
                    int pattern = i * 7 + j * 3 + archetype.Variant;
                    prefab = floorVariantPrefabs[Mathf.Abs(pattern) % floorVariantPrefabs.Length];
                }

                Instantiate(prefab, tileCenter.ToWorld(), Quaternion.identity, parent);
            }
        }
    }

    /// <summary>
    /// Adds interior wall runs that reshape the fixed grid shell. Every run
    /// leaves a four-unit opening aligned with an outer doorway, so all four
    /// neighbouring rooms remain reachable regardless of the chosen shape.
    /// </summary>
    public void BuildInterior(
        Transform parent,
        Vector2Int room,
        DungeonLayout.RoomArchetype archetype
    )
    {
        if (wallPrefab == null || archetype.Shape == DungeonLayout.RoomShape.OpenHall)
            return;

        Vector2 center = DungeonLayout.RoomCenter(room);
        GameObject root = new GameObject($"Interior - {archetype.Shape}");
        root.transform.SetParent(parent, false);

        switch (archetype.Shape)
        {
            case DungeonLayout.RoomShape.Compact:
                BuildHorizontalInterior(root.transform, center, 6f, true);
                BuildHorizontalInterior(root.transform, center, -6f, true);
                BuildVerticalInterior(root.transform, center, 6f, true);
                BuildVerticalInterior(root.transform, center, -6f, true);
                break;
            case DungeonLayout.RoomShape.LargeSquare:
                BuildVerticalInterior(root.transform, center, 10f, true);
                BuildVerticalInterior(root.transform, center, -10f, true);
                break;
            case DungeonLayout.RoomShape.LongHorizontal:
                BuildHorizontalInterior(root.transform, center, 6f, true);
                BuildHorizontalInterior(root.transform, center, -6f, true);
                break;
            case DungeonLayout.RoomShape.LongVertical:
                BuildVerticalInterior(root.transform, center, 6f, true);
                BuildVerticalInterior(root.transform, center, -6f, true);
                break;
            case DungeonLayout.RoomShape.Divided:
                if ((archetype.Variant & 1) == 0)
                    BuildVerticalDivider(root.transform, center);
                else
                    BuildHorizontalDivider(root.transform, center);
                break;
        }
    }

    /// <summary>
    /// Builds one shared wall run between two rooms, with an open gateway or a
    /// blocked (barred) gateway in the middle. The run straddles the room
    /// boundary so both rooms see the same wall.
    /// </summary>
    public GameObject BuildEdge(Transform parent, DungeonEdge edge, bool open)
    {
        GameObject root = new GameObject(
            $"Edge ({edge.X}, {edge.Y}, {(edge.Horizontal ? "H" : "V")})"
        );
        root.transform.SetParent(parent, false);
        Transform wallRun = CreateOcclusionSection(root.transform, "Wall Run");
        DungeonOcclusionSection wallRunSection =
            wallRun.GetComponent<DungeonOcclusionSection>();

        Vector2 roomCenter = DungeonLayout.RoomCenter(new Vector2Int(edge.X, edge.Y));
        GameObject gatePrefab = open ? gateOpenPrefab : gateBlockedPrefab;

        if (edge.Horizontal)
        {
            float boundaryZ = roomCenter.y + HalfRoomDepth;
            wallRunSection.ConfigureEdge(
                new Vector3(roomCenter.x - HalfRoomWidth, 0f, boundaryZ),
                new Vector3(roomCenter.x + HalfRoomWidth, 0f, boundaryZ)
            );
            int gateIndex = DungeonLayout.RoomTilesX / 2;
            GameObject gate = null;
            for (int i = 0; i < DungeonLayout.RoomTilesX; i++)
            {
                float x = roomCenter.x + (i - gateIndex) * Tile;
                if (i == gateIndex)
                {
                    gate = Instantiate(
                        gatePrefab,
                        new Vector3(x, 0f, boundaryZ + WallSlabCenterOffset),
                        Quaternion.identity,
                        wallRun
                    );
                }
                else
                {
                    Instantiate(
                        wallPrefab,
                        new Vector3(x, 0f, boundaryZ),
                        Quaternion.identity,
                        wallRun
                    );
                }
            }
            ConfigureGatewayOcclusion(gate, wallRun);
        }
        else
        {
            float boundaryX = roomCenter.x + HalfRoomWidth;
            wallRunSection.ConfigureEdge(
                new Vector3(boundaryX, 0f, roomCenter.y - HalfRoomDepth),
                new Vector3(boundaryX, 0f, roomCenter.y + HalfRoomDepth)
            );
            int gateIndex = DungeonLayout.RoomTilesZ / 2;
            Quaternion sideways = Quaternion.Euler(0f, 90f, 0f);
            GameObject gate = null;
            for (int j = 0; j < DungeonLayout.RoomTilesZ; j++)
            {
                float z = roomCenter.y + (j - gateIndex) * Tile;
                if (j == gateIndex)
                {
                    gate = Instantiate(
                        gatePrefab,
                        new Vector3(boundaryX + WallSlabCenterOffset, 0f, z),
                        sideways,
                        wallRun
                    );
                }
                else
                {
                    Instantiate(
                        wallPrefab,
                        new Vector3(boundaryX, 0f, z),
                        sideways,
                        wallRun
                    );
                }
            }
            ConfigureGatewayOcclusion(gate, wallRun);
        }

        return root;
    }

    /// <summary>
    /// Keeps perpendicular wall runs linked for coordinated occlusion fading
    /// without placing a visible wall-corner mesh at their shared vertex.
    /// </summary>
    public GameObject BuildJunction(Transform parent, Vector2Int vertex)
    {
        Vector3 position = new Vector3(
            vertex.x * DungeonLayout.RoomWidth + HalfRoomWidth,
            0f,
            vertex.y * DungeonLayout.RoomDepth + HalfRoomDepth
        );
        var junction = new GameObject($"Wall Junction ({vertex.x}, {vertex.y})");
        junction.transform.SetParent(parent, false);
        junction.transform.position = position;
        junction.AddComponent<DungeonOcclusionSection>().ConfigureJunction(position);
        return junction;
    }

    private static void ConfigureGatewayOcclusion(GameObject gate, Transform section)
    {
        DungeonOcclusionSection occlusionSection = section.GetComponent<DungeonOcclusionSection>();
        occlusionSection.ConfigureGateway(gate.transform);

        var volume = new GameObject("Gateway Top Occlusion Volume");
        volume.transform.SetParent(gate.transform, false);
        int wallLayer = LayerMask.NameToLayer("Wall");
        volume.layer = wallLayer >= 0 ? wallLayer : gate.layer;
        volume
            .AddComponent<DungeonOcclusionVolume>()
            .Configure(new Vector3(0f, 2.15f, 0f), new Vector3(3.1f, 1f, 2f));
    }

    private static Vector2 TileCenter(Vector2 roomCenter, int i, int j)
    {
        return new Vector2(
            roomCenter.x + (i - DungeonLayout.RoomTilesX / 2) * Tile,
            roomCenter.y + (j - DungeonLayout.RoomTilesZ / 2) * Tile
        );
    }

    private bool UsesDetailedFloor(
        int i,
        int j,
        DungeonLayout.RoomArchetype archetype,
        System.Random random
    )
    {
        int x = i - DungeonLayout.RoomTilesX / 2;
        int z = j - DungeonLayout.RoomTilesZ / 2;
        bool pattern = archetype.Theme switch
        {
            DungeonLayout.RoomTheme.Empty => false,
            DungeonLayout.RoomTheme.Storage => Mathf.Abs(x) == 3 || Mathf.Abs(z) == 2,
            DungeonLayout.RoomTheme.Banquet => archetype.Shape
            == DungeonLayout.RoomShape.LongHorizontal
                ? z == 0
                : x == 0,
            DungeonLayout.RoomTheme.Armory => (i + j + archetype.Variant) % 2 == 0,
            DungeonLayout.RoomTheme.Shrine => Mathf.Abs(x) == Mathf.Abs(z) || (x == 0 && z == 0),
            DungeonLayout.RoomTheme.Flooded => (i * 2 + j + archetype.Variant) % 3 == 0,
            DungeonLayout.RoomTheme.TreasureVault => Mathf.Abs(x) == 2
                || Mathf.Abs(z) == 2
                || ((i + j) & 1) == 0,
            DungeonLayout.RoomTheme.Collapsed => Mathf.Abs(x - z + archetype.Variant - 1) <= 1,
            _ => false,
        };

        float chance =
            archetype.Theme == DungeonLayout.RoomTheme.Empty
                ? floorVariantChance * 0.25f
                : floorVariantChance;
        return pattern || random.NextDouble() < chance;
    }
}
