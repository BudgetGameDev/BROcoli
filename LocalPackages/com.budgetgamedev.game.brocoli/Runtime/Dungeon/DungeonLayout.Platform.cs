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
        /// The crossing between the platform's two rows. The camera looks over
        /// this run at whoever stands in the north row, so it is built as a
        /// knee-high ledge with full-height masonry only on the grid posts,
        /// instead of a wall the visibility system would have to keep lowering.
        /// </summary>
        RowDivider,
    }

    public sealed partial class DungeonLayout
    {
        private const int PlatformCurveSalt = 1201;
        private const int PlatformDepthInRooms = 2;

        /// <summary>
        /// The dungeon is an endless east-west platform rather than an endless
        /// square grid. Its two-room-deep strip meanders gently by at most one row
        /// between neighbouring columns, keeping north/south travel short while
        /// avoiding a perfectly straight, artificial silhouette.
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
        /// crossing between the two playable rows is the low divider, and other
        /// outer edges remain solid background architecture.
        /// </summary>
        public DungeonEdgeStyle PlayableEdgeStyle(DungeonEdge edge)
        {
            EdgeRooms(edge, out Vector2Int lowerOrLeft, out Vector2Int upperOrRight);
            bool first = IsPlayableRoom(lowerOrLeft);
            bool second = IsPlayableRoom(upperOrRight);
            if (first && second)
                return edge.Horizontal ? DungeonEdgeStyle.RowDivider : DungeonEdgeStyle.Interior;
            if (edge.Horizontal && !first && second)
                return DungeonEdgeStyle.SouthCliff;
            return DungeonEdgeStyle.SolidBoundary;
        }

        /// <summary>
        /// A broad crossing between playable neighbours. East-west joins are
        /// deliberately generous and the single north-south join in each column
        /// is wide, leaving only short wall shoulders near the corners.
        /// </summary>
        public DungeonPassage PlayablePassage(Vector2Int room, int direction)
        {
            Vector2Int neighbour = room + DirectionOffsets[direction];
            if (!IsPlayableRoom(room) || !IsPlayableRoom(neighbour))
                return new DungeonPassage(false, 0, 0);

            DungeonEdge edge = EdgeBetween(room, direction);

            // A merged mega room still has to read as one continuous space. Its
            // internal edges keep the cluster's own passage, which opens every
            // slot between the grid posts; a broad crossing would leave a wall
            // run standing across the middle of the hall.
            if (IsClusterInternalEdge(edge))
                return Passage(edge, true);

            int slots = edge.Horizontal ? RoomTilesX : RoomTilesZ;
            int middle = slots / 2;
            int openingMask = (1 << (middle - 1)) | (1 << middle) | (1 << (middle + 1));
            return new DungeonPassage(true, openingMask, 0);
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
            float phase = Hash(0, 0, PlatformCurveSalt) / (float)uint.MaxValue * Mathf.PI * 2f;
            float origin = Mathf.Sin(phase);
            float wave = Mathf.Sin(phase + x * 0.22f) - origin;
            return Mathf.RoundToInt(Mathf.Clamp(wave, -1f, 1f));
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
