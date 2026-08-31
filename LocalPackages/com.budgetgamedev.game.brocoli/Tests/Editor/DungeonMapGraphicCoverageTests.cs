using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonMapGraphicCoverageTests
    {
        private GameObject mapHost;
        private GameObject dungeonHost;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(mapHost);
            Object.DestroyImmediate(dungeonHost);
        }

        [Test]
        public void MapDrawsRoomsConnectionsAndPlayerMarkerFromDungeonState()
        {
            mapHost = new GameObject(
                "Map",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DungeonMapGraphic)
            );
            var map = mapHost.GetComponent<DungeonMapGraphic>();
            mapHost.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 400f);

            dungeonHost = new GameObject("Dungeon");
            dungeonHost.SetActive(false);
            var dungeon = dungeonHost.AddComponent<DungeonManager>();
            var layout = new DungeonLayout(2468);
            Vector2Int current = FindRoomWithNorthAndEastDoors(layout);
            SetField(dungeon, "layout", layout);
            SetField(dungeon, "currentRoom", current);
            SetField(dungeon, "hasCurrentRoom", true);
            SetField(dungeon, "roomsVisited", 3);
            SetField(map, "dungeon", dungeon);

            using (var empty = new VertexHelper())
                map.PopulateMesh(empty);
            SetField(dungeon, "layout", null);
            using (var noLayout = new VertexHelper())
                map.PopulateMesh(noLayout);
            SetField(dungeon, "layout", layout);

            map.RefreshFromDungeon(true);
            map.RefreshFromDungeon();
            map.FocusPlayer();
            map.Pan(new Vector2(0.5f, -0.25f));
            map.ZoomBy(100f);
            map.ZoomBy(-100f);
            var pointer = new PointerEventData(null)
            {
                delta = new Vector2(20f, -10f),
                scrollDelta = Vector2.up,
            };
            map.OnDrag(pointer);
            map.OnScroll(pointer);

            var visited = GetField<List<Vector2Int>>(map, "visitedRooms");
            var lookup = GetField<HashSet<Vector2Int>>(map, "visitedLookup");
            visited.Clear();
            lookup.Clear();
            AddRoom(current);
            AddRoom(current + DungeonLayout.DirectionOffsets[DungeonLayout.North]);
            AddRoom(current + DungeonLayout.DirectionOffsets[DungeonLayout.East]);
            AddRoom(Vector2Int.zero);

            using var vertices = new VertexHelper();
            map.PopulateMesh(vertices);

            Assert.That(map.Dungeon, Is.SameAs(dungeon));
            Assert.That(vertices.currentVertCount, Is.GreaterThanOrEqualTo(28));

            void AddRoom(Vector2Int room)
            {
                if (lookup.Add(room))
                    visited.Add(room);
            }
        }

        private static Vector2Int FindRoomWithNorthAndEastDoors(DungeonLayout layout)
        {
            for (int x = -10; x <= 10; x++)
            for (int y = -10; y <= 10; y++)
            {
                var room = new Vector2Int(x, y);
                if (
                    layout.IsPlayableDoorOpen(room, DungeonLayout.North)
                    && layout.IsPlayableDoorOpen(room, DungeonLayout.East)
                )
                    return room;
            }
            Assert.Fail("The deterministic layout did not expose a testable corner room.");
            return default;
        }

        private static T GetField<T>(object target, string name) =>
            (T)
                target
                    .GetType()
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(target);

        private static void SetField(object target, string name, object value) =>
            target
                .GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
