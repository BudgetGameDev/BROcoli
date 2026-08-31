using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class DungeonLayout
    {
        private const int PassagePatternSalt = 911;

        // Most doorways are simply a missing wall piece. Only a minority get the
        // decorative arch frame so it stays a highlight instead of the norm.
        private const double ArchwayEdgeChance = 0.22;

        private static readonly int[] HorizontalDoubleOpeningMasks =
        {
            (1 << 1) | (1 << 3),
            (1 << 1) | (1 << 4),
            (1 << 1) | (1 << 5),
            (1 << 2) | (1 << 4),
            (1 << 2) | (1 << 5),
            (1 << 3) | (1 << 5),
        };

        private static readonly int[] HorizontalTripleOpeningMasks =
        {
            (1 << 1) | (1 << 3) | (1 << 5),
            (1 << 1) | (1 << 2) | (1 << 4),
            (1 << 2) | (1 << 4) | (1 << 5),
        };

        /// <summary>
        /// Chooses the repeatable openings for a shared edge. A closed edge is a
        /// solid wall run with no gateway at all; an open edge drops one to three
        /// wall pieces, of which at most one is framed by an archway. An edge
        /// inside a mega-room cluster opens every slot except the two at its grid
        /// posts: the cells read as one continuous space, the remaining stubs
        /// stand as pillars marking the old boundary, and every run meeting a
        /// post stays walled there, which the wall-fade grouping relies on.
        /// </summary>
        public DungeonPassage Passage(DungeonEdge edge, bool open)
        {
            if (!open)
                return new DungeonPassage(false, 0, 0);

            int slotCount = edge.Horizontal ? RoomTilesX : RoomTilesZ;
            if (IsClusterInternalEdge(edge))
            {
                int innerMask = ((1 << slotCount) - 1) & ~(1 | (1 << (slotCount - 1)));
                return new DungeonPassage(true, innerMask, 0);
            }
            int salt = edge.Horizontal ? PassagePatternSalt : PassagePatternSalt + 1;
            var random = new System.Random((int)Hash(edge.X, edge.Y, salt));
            double countRoll = random.NextDouble();
            int openingCount =
                countRoll < 0.55 ? 1
                : countRoll < 0.88 ? 2
                : 3;
            int openingMask = PickOpeningMask(slotCount, openingCount, random);

            int archwayMask =
                random.NextDouble() < ArchwayEdgeChance
                    ? PickSingleArchwayMask(openingMask, slotCount, random)
                    : 0;
            return new DungeonPassage(true, openingMask, archwayMask);
        }

        /// <summary>The four passages around a room, indexed for wall dressing.</summary>
        public RoomDoorways Doorways(Vector2Int room)
        {
            return new RoomDoorways(
                Passage(EdgeBetween(room, North), IsDoorOpen(room, North)),
                Passage(EdgeBetween(room, East), IsDoorOpen(room, East)),
                Passage(EdgeBetween(room, South), IsDoorOpen(room, South)),
                Passage(EdgeBetween(room, West), IsDoorOpen(room, West))
            );
        }

        private static int PickOpeningMask(int slotCount, int openingCount, System.Random random)
        {
            if (openingCount <= 1)
                return 1 << random.Next(1, slotCount - 1);

            if (slotCount == RoomTilesZ)
                return openingCount == 2 ? (1 << 1) | (1 << 3) : (1 << 1) | (1 << 2) | (1 << 3);

            int[] masks =
                openingCount == 2 ? HorizontalDoubleOpeningMasks : HorizontalTripleOpeningMasks;
            return masks[random.Next(masks.Length)];
        }

        /// <summary>
        /// Picks the one opening on this run that gets an arch, from the openings
        /// framed by a wall piece on both sides. The frame is wider than the slot
        /// it stands in, so its posts land on the neighbouring slabs: with an
        /// opening beside it there is nothing for a post to meet and the arch reads
        /// as a free-standing gate in the middle of a gap. Some runs open two
        /// adjacent slots and so have no framed opening at all; those get no arch.
        /// </summary>
        private static int PickSingleArchwayMask(
            int openingMask,
            int slotCount,
            System.Random random
        )
        {
            int framed = FramedOpeningMask(openingMask, slotCount);
            int candidates = SlotCount(framed);
            if (candidates == 0)
                return 0;

            int chosen = random.Next(candidates);
            return PickNthDirectionBit(framed, chosen);
        }

        /// <summary>
        /// The openings that have a wall piece on both sides, and so can carry an
        /// archway. An opening at either end of the run is unframed by definition:
        /// the run stops at the grid post rather than continuing past it.
        /// </summary>
        public static int FramedOpeningMask(int openingMask, int slotCount)
        {
            int framed = 0;
            for (int slot = 1; slot < slotCount - 1; slot++)
            {
                bool open = (openingMask & (1 << slot)) != 0;
                bool leftWalled = (openingMask & (1 << (slot - 1))) == 0;
                bool rightWalled = (openingMask & (1 << (slot + 1))) == 0;
                if (open && leftWalled && rightWalled)
                    framed |= 1 << slot;
            }
            return framed;
        }

        private static int SlotCount(int mask)
        {
            int count = 0;
            uint remaining = unchecked((uint)mask);
            while (remaining != 0)
            {
                count += (int)(remaining & 1);
                remaining >>= 1;
            }
            return count;
        }

        /// <summary>
        /// The passages on a room's four outer walls. Wall-mounted dressing asks
        /// this before hanging anything, so torches and banners never end up
        /// floating in a doorway.
        /// </summary>
        public readonly struct RoomDoorways
        {
            // Anything this far past the room's half extent belongs to the outer
            // wall on that side rather than to an interior wall run.
            private const float OuterWallBand = TileSize * 0.5f;

            public readonly DungeonPassage North;
            public readonly DungeonPassage East;
            public readonly DungeonPassage South;
            public readonly DungeonPassage West;

            public RoomDoorways(
                DungeonPassage north,
                DungeonPassage east,
                DungeonPassage south,
                DungeonPassage west
            )
            {
                North = north;
                East = east;
                South = south;
                West = west;
            }

            /// <summary>
            /// Whether a room-local point on an outer wall falls inside one of that
            /// wall's openings, allowing <paramref name="clearance"/> for the width
            /// of the mounted object.
            /// </summary>
            public bool BlocksDoorway(Vector2 local, float clearance)
            {
                if (local.y >= RoomDepth * 0.5f - OuterWallBand)
                    return North.OverlapsOpening(local.x, RoomTilesX, clearance);
                if (local.y <= -(RoomDepth * 0.5f - OuterWallBand))
                    return South.OverlapsOpening(local.x, RoomTilesX, clearance);
                if (local.x >= RoomWidth * 0.5f - OuterWallBand)
                    return East.OverlapsOpening(local.y, RoomTilesZ, clearance);
                if (local.x <= -(RoomWidth * 0.5f - OuterWallBand))
                    return West.OverlapsOpening(local.y, RoomTilesZ, clearance);
                return false;
            }
        }
    }
}
