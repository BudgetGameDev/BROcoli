using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Dresses deterministic dungeon archetypes with themed prop patterns, loot,
    /// and wall lighting. Layouts range from empty rooms to dense storage,
    /// banquet, armoury, shrine, collapsed, flooded, and treasure rooms.
    /// </summary>
    public partial class DungeonPropPlacer : MonoBehaviour
    {
        private const float HalfRoomWidth = DungeonLayout.RoomWidth / 2f;
        private const float HalfRoomDepth = DungeonLayout.RoomDepth / 2f;

        // A wall slab straddles its own centre line, so its two faces are the same
        // distance out on either side. See DungeonWallPiece.
        private const float WallFrontFaceOffset = -DungeonWallPiece.SlabHalfThickness;
        private const float WallBackFaceOffset = DungeonWallPiece.SlabHalfThickness;

        // Independent obstacles reserve a lane wider than the player's 0.86-unit
        // capsule. This prevents procedural placement from creating tempting gaps
        // that are too narrow to traverse reliably. Deliberate clutter clusters
        // use TightClusterGap instead and read as one impassable obstacle group.
        private const float PropGap = 1.05f;
        private const float TightClusterGap = 0.08f;
        private const float WallSealGap = 0.18f;
        private const float LargePropSeparation = 3.8f;

        internal readonly struct OccupiedSpot
        {
            public readonly Vector2 Position;
            public readonly float Radius;
            public readonly bool Large;
            public readonly bool ReservedForChest;

            public OccupiedSpot(
                Vector2 position,
                float radius,
                bool large,
                bool reservedForChest = false
            )
            {
                Position = position;
                Radius = radius;
                Large = large;
                ReservedForChest = reservedForChest;
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

            BuildVisibilityFriendlyBarriers(parent, center, archetype, random, occupied);
            BuildFeatureWallScreens(parent, center, archetype, random, occupied);

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
                DungeonPropMeasurement measurement = Measure(prefab);
                Vector2 local = ChestSpot(archetype, random, measurement.Radius, occupied);
                int yaw = random.Next(0, 4) * 90;
                occupied.Add(
                    new OccupiedSpot(local, measurement.Radius, measurement.IsLarge, true)
                );

                if (openedChestSlots != null && openedChestSlots.Contains(slot))
                    continue;

                GameObject spawned = SpawnProp(
                    parent,
                    prefab,
                    center + local,
                    GroundPlane.YawRotation(yaw)
                );
                if (spawned == null)
                    continue;
                LootChest chest = spawned.GetComponent<LootChest>();
                if (chest != null)
                {
                    chest.ConfigureForRoom(DungeonLayout.Ring(room));
                    placedChests.Add(new PlacedChest(chest, slot));
                }
            }

            BuildPathwayDressing(parent, center, archetype, random, occupied);
            BuildThemeProps(parent, center, archetype, random, occupied);
            return placedChests;
        }

        /// <summary>
        /// Places lighting appropriate to the room theme. The shell
        /// wall mask says which outer sides still carry full-height walls after
        /// the platform boundary is applied; fittings never hang on the others.
        /// </summary>
        public void BuildAtmosphere(
            Transform parent,
            Vector2Int room,
            DungeonLayout.RoomArchetype archetype,
            DungeonLayout.RoomDoorways roomDoorways,
            System.Random random,
            int shellWallMask = DungeonWallDressing.AllShellWalls
        )
        {
            Vector2 center = DungeonLayout.RoomCenter(room);
            if (torchPrefab != null)
            {
                int torchCount = archetype.Theme switch
                {
                    DungeonLayout.RoomTheme.Empty => 2,
                    DungeonLayout.RoomTheme.Shrine => 4,
                    DungeonLayout.RoomTheme.TreasureVault => 4,
                    DungeonLayout.RoomTheme.Storage => 3 + random.Next(0, 2),
                    DungeonLayout.RoomTheme.Banquet => 3 + random.Next(0, 2),
                    DungeonLayout.RoomTheme.Arena => 6,
                    _ => 2 + random.Next(0, 3),
                };
                List<DungeonWallMount> mounts = DungeonWallDressing.TorchMounts(
                    archetype,
                    roomDoorways,
                    torchCount,
                    random,
                    shellWallMask
                );
                for (int i = 0; i < torchCount && i < mounts.Count; i++)
                {
                    // A wall fitting hangs where its pivot says, so it is placed
                    // rather than stood on the floor like a prop. On an interior
                    // run the wall is half height, so the fitting hangs lower to
                    // stay seated on the masonry.
                    EnrolAsOccluder(
                        Instantiate(
                            torchPrefab,
                            (center + mounts[i].Local).ToWorld(mounts[i].HeightOffset),
                            Quaternion.Euler(0f, mounts[i].Yaw, 0f),
                            parent
                        )
                    );
                }
            }
        }
    }
}
