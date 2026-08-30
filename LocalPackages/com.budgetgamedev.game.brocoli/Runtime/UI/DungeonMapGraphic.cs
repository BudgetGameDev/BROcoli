using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>Procedurally draws visited dungeon rooms without external map art.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DungeonMapGraphic : MaskableGraphic, IDragHandler, IScrollHandler
    {
        private const float MinZoom = 26f;
        private const float MaxZoom = 110f;
        private const float DefaultZoom = 54f;
        private const float RoomFillFraction = 0.72f;
        private static readonly Color VisitedColor = new(0.37f, 0.67f, 0.47f, 0.74f);
        private static readonly Color StartColor = new(0.28f, 0.82f, 0.42f, 0.92f);
        private static readonly Color CurrentColor = new(1f, 0.76f, 0.25f, 0.98f);
        private static readonly Color CorridorColor = new(0.29f, 0.52f, 0.36f, 0.66f);
        private static readonly Color OutlineColor = new(0.78f, 0.94f, 0.82f, 0.8f);

        private readonly List<Vector2Int> visitedRooms = new();
        private readonly HashSet<Vector2Int> visitedLookup = new();
        private DungeonManager dungeon;
        private Vector2 viewCenter;
        private float zoom = DefaultZoom;
        private int cachedVisitedCount = -1;
        private Vector2Int cachedCurrentRoom;
        private bool hasFocused;

        public DungeonManager Dungeon => dungeon;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = true;
            color = Color.white;
        }

        public void RefreshFromDungeon(bool force = false)
        {
            if (dungeon == null)
                dungeon = FindAnyObjectByType<DungeonManager>();
            if (dungeon == null)
                return;

            Vector2Int current = dungeon.CurrentRoom;
            if (
                !force
                && cachedVisitedCount == dungeon.RoomsVisited
                && cachedCurrentRoom == current
            )
                return;

            cachedVisitedCount = dungeon.RoomsVisited;
            cachedCurrentRoom = current;
            dungeon.CopyVisitedRooms(visitedRooms);
            visitedLookup.Clear();
            foreach (Vector2Int room in visitedRooms)
                visitedLookup.Add(room);

            if (!hasFocused && dungeon.HasCurrentRoom)
                FocusPlayer();
            SetVerticesDirty();
        }

        public void FocusPlayer()
        {
            if (dungeon == null)
                dungeon = FindAnyObjectByType<DungeonManager>();
            if (dungeon == null || !dungeon.HasCurrentRoom)
                return;

            viewCenter = dungeon.CurrentRoom;
            hasFocused = true;
            SetVerticesDirty();
        }

        public void Pan(Vector2 roomDelta)
        {
            viewCenter += roomDelta;
            hasFocused = true;
            SetVerticesDirty();
        }

        public void ZoomBy(float normalizedDelta)
        {
            zoom = Mathf.Clamp(zoom * Mathf.Exp(normalizedDelta), MinZoom, MaxZoom);
            SetVerticesDirty();
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 step = RoomStep(zoom);
            viewCenter -= new Vector2(eventData.delta.x / step.x, eventData.delta.y / step.y);
            hasFocused = true;
            SetVerticesDirty();
        }

        public void OnScroll(PointerEventData eventData)
        {
            ZoomBy(eventData.scrollDelta.y * 0.12f);
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (dungeon == null || visitedRooms.Count == 0)
                return;

            Rect area = rectTransform.rect;
            Vector2 step = RoomStep(zoom);
            Vector2 roomSize = step * RoomFillFraction;

            foreach (Vector2Int room in visitedRooms)
            {
                Vector2 center = RoomCenter(area, room, viewCenter, step);
                DrawConnections(vertexHelper, area, room, center, step, roomSize);
            }

            foreach (Vector2Int room in visitedRooms)
            {
                Vector2 center = RoomCenter(area, room, viewCenter, step);
                Color fill =
                    room == dungeon.CurrentRoom ? CurrentColor
                    : room == Vector2Int.zero ? StartColor
                    : VisitedColor;
                AddOutlinedRect(vertexHelper, center, roomSize, fill);

                if (room == dungeon.CurrentRoom)
                    AddQuad(vertexHelper, RectFromCenter(center, Vector2.one * 10f), Color.white);
            }
        }

        private void DrawConnections(
            VertexHelper helper,
            Rect area,
            Vector2Int room,
            Vector2 center,
            Vector2 step,
            Vector2 roomSize
        )
        {
            if (dungeon.Layout == null)
                return;

            DrawConnection(helper, area, room, center, step, roomSize, DungeonLayout.North);
            DrawConnection(helper, area, room, center, step, roomSize, DungeonLayout.East);
        }

        private void DrawConnection(
            VertexHelper helper,
            Rect area,
            Vector2Int room,
            Vector2 center,
            Vector2 step,
            Vector2 roomSize,
            int direction
        )
        {
            Vector2Int neighbour = room + DungeonLayout.DirectionOffsets[direction];
            if (!visitedLookup.Contains(neighbour) || !dungeon.Layout.IsDoorOpen(room, direction))
                return;

            Vector2 other = RoomCenter(area, neighbour, viewCenter, step);
            Vector2 midpoint = (center + other) * 0.5f;
            Vector2 size =
                direction == DungeonLayout.North
                    ? new Vector2(Mathf.Max(5f, roomSize.x * 0.2f), step.y - roomSize.y * 0.55f)
                    : new Vector2(step.x - roomSize.x * 0.55f, Mathf.Max(5f, roomSize.y * 0.2f));
            AddQuad(helper, RectFromCenter(midpoint, size), CorridorColor);
        }

        private static Vector2 RoomStep(float currentZoom)
        {
            return new Vector2(
                currentZoom,
                currentZoom * (DungeonLayout.RoomDepth / DungeonLayout.RoomWidth)
            );
        }

        internal static Vector2 RoomCenter(
            Rect area,
            Vector2Int room,
            Vector2 centerRoom,
            Vector2 step
        )
        {
            return area.center
                + new Vector2((room.x - centerRoom.x) * step.x, (room.y - centerRoom.y) * step.y);
        }

        private static void AddOutlinedRect(
            VertexHelper helper,
            Vector2 center,
            Vector2 size,
            Color fill
        )
        {
            AddQuad(helper, RectFromCenter(center, size + Vector2.one * 4f), OutlineColor);
            AddQuad(helper, RectFromCenter(center, size), fill);
        }

        private static Rect RectFromCenter(Vector2 center, Vector2 size)
        {
            return new Rect(center - size * 0.5f, size);
        }

        private static void AddQuad(VertexHelper helper, Rect rect, Color color)
        {
            int start = helper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = new Vector3(rect.xMin, rect.yMin);
            helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMin, rect.yMax);
            helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMax);
            helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMin);
            helper.AddVert(vertex);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start + 2, start + 3, start);
        }
    }
}
