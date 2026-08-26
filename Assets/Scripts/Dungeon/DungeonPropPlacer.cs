using UnityEngine;

/// <summary>
/// Dresses a dungeon room: maybe a loot chest, scattered decorative props,
/// standing torches near the corners (the dungeon's primary light source),
/// and reflective water pools. All placement comes from the room's
/// deterministic random stream so rooms rebuild identically.
/// </summary>
public class DungeonPropPlacer : MonoBehaviour
{
    private const float HalfRoomWidth = DungeonLayout.RoomWidth / 2f;
    private const float HalfRoomDepth = DungeonLayout.RoomDepth / 2f;

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

    [SerializeField]
    private int maxPropsPerRoom = 5;

    [Header("Atmosphere")]
    [SerializeField]
    private GameObject torchPrefab;

    [SerializeField]
    private GameObject waterPrefab;

    [SerializeField, Range(0f, 1f)]
    private float waterChance = 0.45f;

    /// <summary>
    /// Places the chest (if allowed and rolled) and decorative props.
    /// Returns the spawned chest so the manager can track its opened state.
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
            GameObject spawned = Instantiate(
                prefab,
                InnerSpot(center, random).ToWorld(),
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
                Instantiate(
                    prefab,
                    InnerSpot(center, random).ToWorld(),
                    GroundPlane.YawRotation(random.Next(0, 360)),
                    parent
                );
            }
        }

        return chest;
    }

    /// <summary>Places corner torches and maybe a water pool or two.</summary>
    public void BuildAtmosphere(Transform parent, Vector2Int room, System.Random random)
    {
        Vector2 center = DungeonLayout.RoomCenter(room);

        if (torchPrefab != null)
        {
            // Torch candidates sit just inside the four corners; every room
            // gets at least two so no room is ever fully dark.
            var corners = new[]
            {
                new Vector2(-HalfRoomWidth + 2.8f, -HalfRoomDepth + 2.8f),
                new Vector2(HalfRoomWidth - 2.8f, -HalfRoomDepth + 2.8f),
                new Vector2(-HalfRoomWidth + 2.8f, HalfRoomDepth - 2.8f),
                new Vector2(HalfRoomWidth - 2.8f, HalfRoomDepth - 2.8f),
            };
            Shuffle(corners, random);
            int torchCount = 2 + random.Next(0, 3);
            for (int i = 0; i < torchCount && i < corners.Length; i++)
            {
                Instantiate(
                    torchPrefab,
                    (center + corners[i]).ToWorld(),
                    GroundPlane.YawRotation(random.Next(0, 360)),
                    parent
                );
            }
        }

        if (waterPrefab != null && random.NextDouble() < waterChance)
        {
            int poolCount = 1 + random.Next(0, 2);
            for (int i = 0; i < poolCount; i++)
            {
                GameObject pool = Instantiate(
                    waterPrefab,
                    InnerSpot(center, random).ToWorld(0.02f),
                    GroundPlane.YawRotation(random.Next(0, 360)),
                    parent
                );
                float scale = 0.7f + (float)random.NextDouble() * 0.9f;
                pool.transform.localScale *= scale;
            }
        }
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

    private static void Shuffle(Vector2[] values, System.Random random)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
