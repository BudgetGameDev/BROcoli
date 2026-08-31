using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Pure deterministic layout math for the infinite dungeon. Every query is a
    /// function of the run seed and the room coordinate, so any room can be
    /// regenerated identically after being unloaded, and both rooms beside a
    /// doorway always agree on whether it is open or blocked.
    /// </summary>
    public sealed partial class DungeonLayout
    {
        public const float TileSize = 4f;
        public const int RoomTilesX = 7;
        public const int RoomTilesZ = 5;
        public const float RoomWidth = TileSize * RoomTilesX;
        public const float RoomDepth = TileSize * RoomTilesZ;

        public const int North = 0;
        public const int East = 1;
        public const int South = 2;
        public const int West = 3;

        public static readonly Vector2Int[] DirectionOffsets =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
        };

        // High enough that most rooms have several ways through; the forced-door
        // rules below then make single-door dead ends genuinely rare.
        private const float DoorOpenChance = 0.7f;
        private const float SecondDoorChance = 0.7f;
        private const int EdgeSalt = 101;
        private const int ForcedDoorSalt = 202;
        private const int SecondDoorSalt = 203;
        private const int DeadEndBreakSalt = 204;

        private readonly int seed;

        public DungeonLayout(int seed)
        {
            this.seed = seed;
        }

        /// <summary>Ground-plane centre of a room.</summary>
        public static Vector2 RoomCenter(Vector2Int room)
        {
            return new Vector2(room.x * RoomWidth, room.y * RoomDepth);
        }

        /// <summary>The room containing a ground-plane position.</summary>
        public static Vector2Int RoomAt(Vector2 groundPosition)
        {
            return new Vector2Int(
                Mathf.RoundToInt(groundPosition.x / RoomWidth),
                Mathf.RoundToInt(groundPosition.y / RoomDepth)
            );
        }

        /// <summary>Chebyshev ring distance from the starting room.</summary>
        public static int Ring(Vector2Int room)
        {
            return Mathf.Max(Mathf.Abs(room.x), Mathf.Abs(room.y));
        }

        public static DungeonEdge EdgeBetween(Vector2Int room, int direction)
        {
            return direction switch
            {
                North => new DungeonEdge(room.x, room.y, true),
                South => new DungeonEdge(room.x, room.y - 1, true),
                East => new DungeonEdge(room.x, room.y, false),
                _ => new DungeonEdge(room.x - 1, room.y, false),
            };
        }

        /// <summary>
        /// Whether the doorway on the given side of a room is open. An edge inside
        /// a mega-room cluster is always open. Otherwise a door is open when its
        /// edge rolls open, or when either adjacent room forces it open so it is
        /// not sealed in or left as a dead end (see <see cref="ForcedDoorMask"/>).
        /// Every room always keeps at least one active door.
        /// </summary>
        public bool IsDoorOpen(Vector2Int room, int direction)
        {
            DungeonEdge edge = EdgeBetween(room, direction);
            if (IsClusterInternalEdge(edge))
                return true;
            if (IsEdgeBaseOpen(edge))
                return true;
            if ((ForcedDoorMask(room) & (1 << direction)) != 0)
                return true;

            Vector2Int neighbour = room + DirectionOffsets[direction];
            int opposite = (direction + 2) % 4;
            return (ForcedDoorMask(neighbour) & (1 << opposite)) != 0;
        }

        /// <summary>Deterministic per-room random stream for content decisions.</summary>
        public System.Random RoomRandom(Vector2Int room, int salt)
        {
            return new System.Random((int)Hash(room.x, room.y, salt));
        }

        /// <summary>What kind of enemy group a room holds.</summary>
        public readonly struct RoomPopulation
        {
            public const int SpiderSwarmSize = 5;

            public readonly int Count;
            public readonly bool Elite;
            public readonly bool IsSpiderSwarm;

            public RoomPopulation(int count, bool elite, bool isSpiderSwarm = false)
            {
                Count = count;
                Elite = elite;
                IsSpiderSwarm = isSpiderSwarm;
            }

            public static RoomPopulation SpiderSwarm()
            {
                return new RoomPopulation(SpiderSwarmSize, false, true);
            }
        }

        public enum RoomShape
        {
            OpenHall,
            GrandArena,
            Tiny,
            Compact,
            NarrowHorizontal,
            NarrowVertical,
            LargeSquare,
            LongHorizontal,
            LongVertical,
            DiagonalGallery,
            Divided,

            /// <summary>
            /// One cell of a merged mega room: a full open shell whose
            /// cluster-internal edges build no wall at all, so several cells read
            /// as one long, deep, or huge chamber.
            /// </summary>
            MegaSection,
        }

        public enum RoomTheme
        {
            Empty,
            Sparse,
            Storage,
            Banquet,
            Armory,
            Shrine,
            Flooded,
            TreasureVault,
            Collapsed,
            Arena,
        }

        /// <summary>
        /// A room's deterministic visual and gameplay profile. The outer room grid
        /// never changes (so shared doorways remain compatible), while interior
        /// walls turn that shell into compact, long, square, or divided spaces.
        /// </summary>
        public readonly struct RoomArchetype
        {
            public readonly RoomShape Shape;
            public readonly RoomTheme Theme;
            public readonly float HalfWidth;
            public readonly float HalfDepth;
            public readonly int Variant;

            public RoomArchetype(
                RoomShape shape,
                RoomTheme theme,
                float halfWidth,
                float halfDepth,
                int variant
            )
            {
                Shape = shape;
                Theme = theme;
                HalfWidth = halfWidth;
                HalfDepth = halfDepth;
                Variant = variant;
            }

            public int EnemyCapacity =>
                Shape switch
                {
                    // Tiny and compact rooms hold more than their floor space
                    // suggests, so a swarm roll can genuinely cram them full.
                    RoomShape.Tiny => 6,
                    RoomShape.Compact => 8,
                    RoomShape.MegaSection => 16,
                    RoomShape.NarrowHorizontal => 6,
                    RoomShape.NarrowVertical => 6,
                    RoomShape.LongHorizontal => 8,
                    RoomShape.LongVertical => 8,
                    RoomShape.DiagonalGallery => 10,
                    RoomShape.Divided => 10,
                    RoomShape.GrandArena => 20,
                    RoomShape.OpenHall => 14,
                    _ => 12,
                };

            public override string ToString()
            {
                return $"{Shape} / {Theme}";
            }
        }
    }
}
