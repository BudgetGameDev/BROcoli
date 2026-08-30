using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Regression guard against pieces clipping into a shared, fighting surface.
    /// The kit assembles by interpenetration: crossing slabs bury each other's
    /// end caps, gates swallow wall ends inside their own posts, seam caps hide
    /// inside butt joints. That is fine, because none of it is ever seen. What flickers is
    /// two different pieces drawing the same plane - the shimmer at the base of
    /// crossing wall runs was their identical floor decals fighting - which is
    /// why walls and gates are seated at slightly different heights. These tests
    /// pin that invariant from two directions: no shared up-facing area at all,
    /// and nothing coplanar anywhere else unless the joint is sealed or buried.
    /// </summary>
    public sealed partial class DungeonBuiltClippingTests
    {
        private const string WallPrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonWall.prefab";
        private const string GatePrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonGateOpen.prefab";
        private const string FloorPrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonFloor.prefab";

        private GameObject host;
        private GameObject root;
        private DungeonRoomBuilder builder;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("BuilderHost");
            root = new GameObject("BuiltRoom");
            builder = host.AddComponent<DungeonRoomBuilder>();
            var serialized = new SerializedObject(builder);
            SetPrefab(serialized, "wallPrefab", WallPrefabPath);
            SetPrefab(serialized, "gateOpenPrefab", GatePrefabPath);
            SetPrefab(serialized, "floorPrefab", FloorPrefabPath);
            serialized.ApplyModifiedProperties();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
            if (host != null)
                Object.DestroyImmediate(host);
        }

        /// <summary>
        /// The strict guard for what the top-down camera looks at: no two pieces
        /// may share up-facing area on one plane, full stop. Up-facing surfaces
        /// have nothing above them to hide behind, so there is no sealed-joint
        /// allowance here - this is the test that fails when a seating height
        /// regresses and floor decals or ledges start fighting again.
        /// </summary>
        [Test]
        public void BuiltPiecesNeverShareCoplanarUpFacingSurface()
        {
            ForEachBuiltRoom(
                (seed, room) =>
                {
                    List<string> clashes = CoplanarUpFacingOverlaps(root.transform);
                    Assert.That(
                        clashes,
                        Is.Empty,
                        $"seed {seed}: room {room} built pieces sharing coplanar up-facing "
                            + "surface, which z-fights:\n"
                            + string.Join("\n", clashes)
                    );
                }
            );
        }

        /// <summary>
        /// The broad guard for every other orientation: pieces may interpenetrate,
        /// but every coplanar contact must be a sealed butt joint or buried inside
        /// a piece's body. Anything else is exposed and fights - a piece built
        /// twice, a run misplaced onto another's face, a frame whose shell lands
        /// exactly on a neighbour's cap.
        /// </summary>
        [Test]
        public void BuiltPiecesOnlyShareCoplanarSurfaceInsideSealedJoints()
        {
            ForEachBuiltRoom(
                (seed, room) =>
                {
                    List<string> clashes = ExposedCoplanarOverlaps(root.transform);
                    Assert.That(
                        clashes,
                        Is.Empty,
                        $"seed {seed}: room {room} built pieces sharing exposed coplanar "
                            + "surface, which z-fights:\n"
                            + string.Join("\n", clashes)
                    );
                }
            );
        }

        private void ForEachBuiltRoom(System.Action<int, Vector2Int> check)
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                foreach (Vector2Int room in Rooms())
                {
                    DungeonLayout.RoomArchetype archetype = layout.Archetype(room);
                    builder.BuildFloor(root.transform, room, archetype, new System.Random(seed));
                    builder.BuildInterior(root.transform, room, archetype);
                    for (int direction = 0; direction < 4; direction++)
                    {
                        DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
                        builder.BuildEdge(
                            root.transform,
                            edge,
                            layout.Passage(edge, layout.IsDoorOpen(room, direction))
                        );
                    }

                    check(seed, room);

                    // Collect first: destroying while enumerating a Transform skips
                    // siblings and would leave the next room dirty.
                    var spent = new List<GameObject>();
                    foreach (Transform child in root.transform)
                        spent.Add(child.gameObject);
                    foreach (GameObject child in spent)
                        Object.DestroyImmediate(child);
                }
            }
        }

        private static void SetPrefab(SerializedObject serialized, string field, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            serialized.FindProperty(field).objectReferenceValue = prefab;
        }

        private static IEnumerable<Vector2Int> Rooms()
        {
            yield return Vector2Int.zero;
            yield return new Vector2Int(1, 0);
            yield return new Vector2Int(0, 1);
            yield return new Vector2Int(-2, 3);
            yield return new Vector2Int(5, -4);
        }
    }
}
