using UnityEngine;

/// <summary>
/// Instantiates the physical pieces of a dungeon room: floor tiles, shared
/// wall runs with open or blocked gateways, corner posts, decorative props,
/// and loot chests. Pure construction; the layout decisions come from
/// <see cref="DungeonLayout"/> and the per-room random streams handed in by
/// <see cref="DungeonManager"/>.
/// </summary>
public class DungeonRoomBuilder : MonoBehaviour
{
    private const float Tile = DungeonLayout.TileSize;
    private const float HalfRoomWidth = DungeonLayout.RoomWidth / 2f;
    private const float HalfRoomDepth = DungeonLayout.RoomDepth / 2f;

    [Header("Modular Dungeon Kit pieces")]
    [SerializeField]
    private GameObject floorPrefab;

    [SerializeField]
    private GameObject[] floorVariantPrefabs;

    [SerializeField]
    private GameObject wallPrefab;

    [SerializeField]
    private GameObject cornerPrefab;

    [SerializeField]
    private GameObject gateOpenPrefab;

    [SerializeField]
    private GameObject gateBlockedPrefab;

    [Header("Loot and props")]
    [SerializeField]
    private GameObject chestPrefab;

    [SerializeField]
    private GameObject goldenChestPrefab;

    [SerializeField]
    private GameObject[] propPrefabs;

    [SerializeField, Range(0f, 1f)]
    private float floorVariantChance = 0.18f;

    [SerializeField, Range(0f, 1f)]
    private float chestChance = 0.35f;

    [SerializeField, Range(0f, 1f)]
    private float goldenChestChance = 0.12f;

    [SerializeField]
    private int maxPropsPerRoom = 4;

    /// <summary>Builds the 7x5 floor of a room under the given parent.</summary>
    public void BuildFloor(Transform parent, Vector2Int room, System.Random random)
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
                    && random.NextDouble() < floorVariantChance
                )
                {
                    prefab = floorVariantPrefabs[random.Next(floorVariantPrefabs.Length)];
                }

                Instantiate(prefab, tileCenter.ToWorld(), Quaternion.identity, parent);
            }
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

        Vector2 roomCenter = DungeonLayout.RoomCenter(new Vector2Int(edge.X, edge.Y));
        GameObject gatePrefab = open ? gateOpenPrefab : gateBlockedPrefab;

        if (edge.Horizontal)
        {
            float boundaryZ = roomCenter.y + HalfRoomDepth;
            int gateIndex = DungeonLayout.RoomTilesX / 2;
            for (int i = 0; i < DungeonLayout.RoomTilesX; i++)
            {
                float x = roomCenter.x + (i - gateIndex) * Tile;
                if (i == gateIndex)
                {
                    // The gate mesh is centred on its pivot; place it on the line.
                    Instantiate(
                        gatePrefab,
                        new Vector3(x, 0f, boundaryZ),
                        Quaternion.identity,
                        root.transform
                    );
                }
                else
                {
                    // The wall mesh spans local z in [-2, 0]; offsetting the pivot
                    // by +1 makes the piece straddle the boundary line evenly.
                    Instantiate(
                        wallPrefab,
                        new Vector3(x, 0f, boundaryZ + 1f),
                        Quaternion.identity,
                        root.transform
                    );
                }
            }
        }
        else
        {
            float boundaryX = roomCenter.x + HalfRoomWidth;
            int gateIndex = DungeonLayout.RoomTilesZ / 2;
            Quaternion sideways = Quaternion.Euler(0f, 90f, 0f);
            for (int j = 0; j < DungeonLayout.RoomTilesZ; j++)
            {
                float z = roomCenter.y + (j - gateIndex) * Tile;
                if (j == gateIndex)
                {
                    Instantiate(
                        gatePrefab,
                        new Vector3(boundaryX, 0f, z),
                        sideways,
                        root.transform
                    );
                }
                else
                {
                    // Rotated 90 degrees, local +z points along world +x, so the
                    // same +1 pivot offset straddles the vertical boundary.
                    Instantiate(
                        wallPrefab,
                        new Vector3(boundaryX + 1f, 0f, z),
                        sideways,
                        root.transform
                    );
                }
            }
        }

        return root;
    }

    /// <summary>
    /// Builds the post that caps the vertex where four rooms meet, hiding the
    /// spot where perpendicular wall runs cross.
    /// </summary>
    public GameObject BuildCorner(Transform parent, Vector2Int vertex)
    {
        Vector3 position = new Vector3(
            vertex.x * DungeonLayout.RoomWidth + HalfRoomWidth,
            0f,
            vertex.y * DungeonLayout.RoomDepth + HalfRoomDepth
        );
        GameObject corner = Instantiate(cornerPrefab, position, Quaternion.identity, parent);
        corner.name = $"Corner ({vertex.x}, {vertex.y})";
        return corner;
    }

    /// <summary>
    /// Scatters decorative props along the room's inner ring and maybe places
    /// a loot chest. Returns the spawned chest, if any, so the manager can
    /// track its opened state.
    /// </summary>
    public LootChest BuildContents(
        Transform parent,
        Vector2Int room,
        System.Random random,
        bool allowChest
    )
    {
        Vector2 center = DungeonLayout.RoomCenter(room);
        LootChest chest = null;

        if (allowChest && chestPrefab != null && random.NextDouble() < chestChance)
        {
            bool golden = goldenChestPrefab != null && random.NextDouble() < goldenChestChance;
            GameObject prefab = golden ? goldenChestPrefab : chestPrefab;
            Vector2 chestSpot = InnerSpot(center, random);
            GameObject spawned = Instantiate(
                prefab,
                chestSpot.ToWorld(),
                GroundPlane.YawRotation(random.Next(0, 360)),
                parent
            );
            chest = spawned.GetComponent<LootChest>();
        }

        if (propPrefabs != null && propPrefabs.Length > 0)
        {
            int propCount = random.Next(0, maxPropsPerRoom + 1);
            for (int i = 0; i < propCount; i++)
            {
                GameObject prefab = propPrefabs[random.Next(propPrefabs.Length)];
                Vector2 spot = InnerSpot(center, random);
                Instantiate(
                    prefab,
                    spot.ToWorld(),
                    GroundPlane.YawRotation(random.Next(0, 360)),
                    parent
                );
            }
        }

        return chest;
    }

    private static Vector2 TileCenter(Vector2 roomCenter, int i, int j)
    {
        return new Vector2(
            roomCenter.x + (i - DungeonLayout.RoomTilesX / 2) * Tile,
            roomCenter.y + (j - DungeonLayout.RoomTilesZ / 2) * Tile
        );
    }

    /// <summary>
    /// A random spot inside the room that keeps clear of the walls, the
    /// doorway lanes, and the room centre where the player walks in.
    /// </summary>
    private static Vector2 InnerSpot(Vector2 roomCenter, System.Random random)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float x = Mathf.Lerp(
                -HalfRoomWidth + 3.5f,
                HalfRoomWidth - 3.5f,
                (float)random.NextDouble()
            );
            float z = Mathf.Lerp(
                -HalfRoomDepth + 3.5f,
                HalfRoomDepth - 3.5f,
                (float)random.NextDouble()
            );

            // The doorway lanes cross the room through its centre; keeping
            // props out of both lanes also keeps the walk-in area clear.
            if (Mathf.Abs(x) >= 2.5f && Mathf.Abs(z) >= 2.5f)
                return roomCenter + new Vector2(x, z);
        }

        return roomCenter + new Vector2(HalfRoomWidth - 4f, HalfRoomDepth - 4f);
    }
}
