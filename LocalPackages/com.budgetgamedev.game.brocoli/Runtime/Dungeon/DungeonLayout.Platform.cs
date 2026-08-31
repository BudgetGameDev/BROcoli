using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>How a generated edge presents itself in the playable platform.</summary>
    public enum DungeonEdgeStyle
    {
        Interior,
        SolidBoundary,
        SouthCliff,

        /// <summary>
        /// A completely open crossing between the platform's two playable rows.
        /// It builds no wall pieces, including at grid corners, because playable
        /// floor exists on both sides of the east-west edge.
        /// </summary>
        OpenCrossing,
    }

    public sealed partial class DungeonLayout
    {
        private const int PlatformCurveSalt = 1201;
        private const int EnvironmentCycleSalt = 1203;
        private const int PlatformDepthInRooms = 2;
        private const int PlatformDiagonalRun = 12;

        public const int RoomsPerEnvironmentTheme = 20;
        public const int ColumnsPerEnvironmentTheme =
            RoomsPerEnvironmentTheme / PlatformDepthInRooms;

        /// <summary>
        /// Broad environment at a room coordinate. Ten consecutive columns share
        /// one theme; because the platform is two rooms deep, that is a run of
        /// about twenty rooms. The starting band is always Dungeon so existing
        /// runs begin in a fully dressed environment, while the remaining five
        /// themes are shuffled deterministically for each run seed.
        /// </summary>
        public EnvironmentTheme EnvironmentAt(Vector2Int room)
        {
            int segment = EnvironmentSegmentAtColumn(room.x);
            return environmentCycle[PositiveModulo(segment, environmentCycle.Length)];
        }

        /// <summary>The environment belonging to the playable side of an edge.</summary>
        public EnvironmentTheme EnvironmentAt(DungeonEdge edge)
        {
            EdgeRooms(edge, out Vector2Int first, out Vector2Int second);
            if (IsPlayableRoom(first))
                return EnvironmentAt(first);
            if (IsPlayableRoom(second))
                return EnvironmentAt(second);
            return EnvironmentAt(first);
        }

        public static int EnvironmentSegmentAtColumn(int x)
        {
            int centred = x + ColumnsPerEnvironmentTheme / 2;
            return FloorDivide(centred, ColumnsPerEnvironmentTheme);
        }

        /// <summary>
        /// The dungeon is an endless east-west platform rather than an endless
        /// square grid. Its two-room-deep strip follows long diagonal stair-step
        /// runs, turning after a seeded interval. Neighbouring columns still move
        /// by exactly one row, so the strip remains continuously connected while
        /// reading as a diagonal route rather than a horizontal corridor.
        /// </summary>
        public bool IsPlayableRoom(Vector2Int room)
        {
            int south = SouthRoomY(room.x);
            return room.y >= south && room.y < south + PlatformDepthInRooms;
        }

        /// <summary>The nearest cell in the playable strip at this x coordinate.</summary>
        public Vector2Int ClampToPlayableBand(Vector2Int room)
        {
            int south = SouthRoomY(room.x);
            return new Vector2Int(room.x, Mathf.Clamp(room.y, south, south + 1));
        }

        /// <summary>
        /// Resolves the edge policy used by the runtime generator. A boundary
        /// below its playable room is the camera-facing cliff, the horizontal
        /// crossing between the two playable rows is fully open between its grid
        /// posts, and other outer edges remain solid background architecture.
        /// </summary>
        public DungeonEdgeStyle PlayableEdgeStyle(DungeonEdge edge)
        {
            EdgeRooms(edge, out Vector2Int lowerOrLeft, out Vector2Int upperOrRight);
            bool first = IsPlayableRoom(lowerOrLeft);
            bool second = IsPlayableRoom(upperOrRight);
            if (first && second)
                return edge.Horizontal ? DungeonEdgeStyle.OpenCrossing : DungeonEdgeStyle.Interior;
            if (edge.Horizontal && !first && second)
                return DungeonEdgeStyle.SouthCliff;
            return DungeonEdgeStyle.SolidBoundary;
        }

        /// <summary>
        /// A broad crossing between playable neighbours. East-west wall runs can
        /// safely remain around vertical crossings. Horizontal crossings open
        /// every slot, including the grid corners, because playable floor lies on
        /// both sides and any east-west wall there would hide the north side.
        /// </summary>
        public DungeonPassage PlayablePassage(Vector2Int room, int direction)
        {
            Vector2Int neighbour = room + DirectionOffsets[direction];
            if (!IsPlayableRoom(room) || !IsPlayableRoom(neighbour))
                return new DungeonPassage(false, 0, 0);

            DungeonEdge edge = EdgeBetween(room, direction);
            int slots = edge.Horizontal ? RoomTilesX : RoomTilesZ;

            // No part of a horizontal edge survives when both rooms are playable.
            // Even the traditional grid-post pieces are long east-west slabs that
            // a player can stand behind, so open the complete run.
            if (edge.Horizontal)
                return new DungeonPassage(true, (1 << slots) - 1, 0);

            // A merged mega room still has to read as one continuous space. Its
            // internal edges keep the cluster's own passage, which opens every
            // slot between the grid posts; a broad crossing would leave a wall
            // run standing across the middle of the hall.
            if (IsClusterInternalEdge(edge))
                return Passage(edge, true);

            int middle = slots / 2;
            int openingMask = (1 << (middle - 1)) | (1 << middle) | (1 << (middle + 1));
            return new DungeonPassage(true, openingMask, 0);
        }

        /// <summary>
        /// Which sides of a room keep their full-height shell walls once the
        /// platform boundary is applied, as bits of (1 &lt;&lt; direction).
        /// Boundary and crossing edges build railings, rock lines, or nothing
        /// at all, so wall fittings must not hang there.
        /// </summary>
        public int ShellWallMask(Vector2Int room)
        {
            int mask = 0;
            for (int direction = 0; direction < 4; direction++)
            {
                DungeonEdge edge = EdgeBetween(room, direction);
                if (PlayableEdgeStyle(edge) == DungeonEdgeStyle.Interior)
                    mask |= 1 << direction;
            }
            return mask;
        }

        /// <summary>
        /// Whether the built world actually lets the player walk out of this room
        /// on this side. The platform seals its own boundary, so this - not
        /// <see cref="IsDoorOpen"/>, which describes the unbounded grid - is what
        /// navigation and the map have to ask.
        /// </summary>
        public bool IsPlayableDoorOpen(Vector2Int room, int direction)
        {
            return PlayablePassage(room, direction).Open;
        }

        /// <summary>The runtime passages after the platform boundary is applied.</summary>
        public RoomDoorways PlayableDoorways(Vector2Int room)
        {
            return new RoomDoorways(
                PlayablePassage(room, North),
                PlayablePassage(room, East),
                PlayablePassage(room, South),
                PlayablePassage(room, West)
            );
        }

        private int SouthRoomY(int x)
        {
            int period = PlatformDiagonalRun * 2;
            int phase = (int)(Hash(0, 0, PlatformCurveSalt) % (uint)period);
            return DiagonalStep(x + phase) - DiagonalStep(phase);
        }

        private static int DiagonalStep(int value)
        {
            int period = PlatformDiagonalRun * 2;
            int wrapped = value % period;
            if (wrapped < 0)
                wrapped += period;
            return wrapped <= PlatformDiagonalRun ? wrapped : period - wrapped;
        }

        private EnvironmentTheme[] BuildEnvironmentCycle()
        {
            var cycle = new[]
            {
                EnvironmentTheme.Dungeon,
                EnvironmentTheme.Cave,
                EnvironmentTheme.Plains,
                EnvironmentTheme.Forest,
                EnvironmentTheme.Mountain,
                EnvironmentTheme.Desert,
            };
            var random = new System.Random((int)Hash(0, 0, EnvironmentCycleSalt));
            for (int i = cycle.Length - 1; i > 1; i--)
            {
                int other = random.Next(1, i + 1);
                (cycle[i], cycle[other]) = (cycle[other], cycle[i]);
            }
            return cycle;
        }

        private static int FloorDivide(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }

        private static void EdgeRooms(
            DungeonEdge edge,
            out Vector2Int lowerOrLeft,
            out Vector2Int upperOrRight
        )
        {
            lowerOrLeft = new Vector2Int(edge.X, edge.Y);
            upperOrRight = lowerOrLeft + (edge.Horizontal ? Vector2Int.up : Vector2Int.right);
        }
    }
}
