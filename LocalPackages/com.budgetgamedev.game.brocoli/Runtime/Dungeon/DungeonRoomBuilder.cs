using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Instantiates the physical pieces of a dungeon room: floor tiles, shared
    /// wall runs with doorways cut into them, decorative props, and loot
    /// chests. Pure construction; the layout decisions come from
    /// <see cref="DungeonLayout"/> and the per-room random streams handed in by
    /// <see cref="DungeonManager"/>.
    /// </summary>
    public partial class DungeonRoomBuilder : MonoBehaviour
    {
        private const float Tile = DungeonLayout.TileSize;
        private const float HalfRoomWidth = DungeonLayout.RoomWidth / 2f;
        private const float HalfRoomDepth = DungeonLayout.RoomDepth / 2f;

        /// <summary>
        /// Interior dividers are waist-high masonry, never room-height walls.
        /// Keeping this as a construction invariant means a new room shape cannot
        /// accidentally put an opaque shell wall through playable floor.
        /// </summary>
        public const float InteriorWallHeightScale = 0.46f;

        [Header("Modular Dungeon Kit pieces")]
        [SerializeField]
        private GameObject floorPrefab;

        [SerializeField]
        private GameObject[] floorVariantPrefabs;

        [SerializeField]
        private GameObject wallPrefab;

        [SerializeField]
        private GameObject gateOpenPrefab;

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
                        prefab = floorVariantPrefabs[
                            Mathf.Abs(pattern) % floorVariantPrefabs.Length
                        ];
                    }

                    Instantiate(prefab, tileCenter.ToWorld(), Quaternion.identity, parent);
                }
            }
        }

        /// <summary>
        /// Adds the interior wall runs that reshape the fixed grid shell, exactly
        /// where <see cref="DungeonRoomGeometry"/> planned them.
        /// </summary>
        public void BuildInterior(
            Transform parent,
            Vector2Int room,
            DungeonLayout.RoomArchetype archetype
        )
        {
            if (wallPrefab == null)
                return;

            interiorWalls.Clear();
            DungeonRoomGeometry.AppendInteriorWalls(interiorWalls, room, archetype);
            if (interiorWalls.Count == 0)
                return;

            GameObject root = new GameObject($"Interior - {archetype.Shape}");
            root.transform.SetParent(parent, false);
            InstantiateWallRuns(root.transform, interiorWalls, room);
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
                    is DungeonLayout.RoomShape.LongHorizontal
                        or DungeonLayout.RoomShape.NarrowHorizontal
                    ? z == 0
                    : x == 0,
                DungeonLayout.RoomTheme.Armory => (i + j + archetype.Variant) % 2 == 0,
                DungeonLayout.RoomTheme.Shrine => Mathf.Abs(x) == Mathf.Abs(z)
                    || (x == 0 && z == 0),
                DungeonLayout.RoomTheme.Flooded => (i * 2 + j + archetype.Variant) % 3 == 0,
                DungeonLayout.RoomTheme.TreasureVault => Mathf.Abs(x) == 2
                    || Mathf.Abs(z) == 2
                    || ((i + j) & 1) == 0,
                DungeonLayout.RoomTheme.Collapsed => Mathf.Abs(x - z + archetype.Variant - 1) <= 1,
                DungeonLayout.RoomTheme.Arena => Mathf.Abs(x) == 3 || Mathf.Abs(z) == 2,
                _ => false,
            };

            float chance =
                archetype.Theme == DungeonLayout.RoomTheme.Empty
                    ? floorVariantChance * 0.25f
                    : floorVariantChance;
            return pattern || random.NextDouble() < chance;
        }
    }
}
