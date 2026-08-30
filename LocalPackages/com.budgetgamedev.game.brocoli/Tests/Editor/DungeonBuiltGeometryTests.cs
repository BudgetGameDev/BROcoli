using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Ties the plan to what actually gets built. Every other geometry test reasons
    /// about <see cref="DungeonRoomGeometry"/> alone, which cannot see a mistake
    /// made while instantiating - a wrong rotation, say, which leaves a wall
    /// looking right but colliding somewhere else. This builds real prefabs from
    /// the plan and checks the colliders land where the plan said.
    /// </summary>
    public sealed class DungeonBuiltGeometryTests
    {
        private const string WallPrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonWall.prefab";
        private const string GatePrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonGateOpen.prefab";
        private const float Tolerance = 0.02f;

        private GameObject host;
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("BuilderHost");
            root = new GameObject("BuiltRoom");
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
            if (host != null)
                Object.DestroyImmediate(host);
        }

        [Test]
        public void BuiltWallCollidersLandOnTheirPlannedFootprints()
        {
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
            var gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefabPath);
            Assert.That(wallPrefab, Is.Not.Null, WallPrefabPath);
            Assert.That(gatePrefab, Is.Not.Null, GatePrefabPath);

            DungeonRoomBuilder builder = host.AddComponent<DungeonRoomBuilder>();
            var serialized = new SerializedObject(builder);
            serialized.FindProperty("wallPrefab").objectReferenceValue = wallPrefab;
            serialized.FindProperty("gateOpenPrefab").objectReferenceValue = gatePrefab;
            serialized.ApplyModifiedProperties();

            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                foreach (Vector2Int room in Rooms())
                {
                    var model = new DungeonGeometryModel(seed, room, 0);
                    var planned = new List<Rect>();
                    planned.AddRange(
                        model.InteriorWalls(room).ConvertAll(piece => piece.Footprint)
                    );
                    builder.BuildInterior(root.transform, room, model.Layout.Archetype(room));

                    for (int direction = 0; direction < 4; direction++)
                    {
                        DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
                        DungeonPassage passage = model.Passage(room, direction);
                        planned.AddRange(
                            model.EdgeWalls(room, direction).ConvertAll(piece => piece.Footprint)
                        );
                        builder.BuildEdge(root.transform, edge, passage);
                    }

                    List<Rect> built = BuiltWallFootprints(root.transform);
                    Assert.That(
                        built.Count,
                        Is.EqualTo(planned.Count),
                        $"seed {seed}: room {room} built {built.Count} walls for {planned.Count} "
                            + "planned pieces"
                    );
                    foreach (Rect plan in planned)
                    {
                        Assert.That(
                            built.Exists(actual => Matches(actual, plan)),
                            $"seed {seed}: room {room} planned a wall at {plan} but built none there"
                        );
                    }

                    // Collect first: destroying while enumerating a Transform skips
                    // siblings and would leave the next room's count wrong.
                    var spent = new List<GameObject>();
                    foreach (Transform child in root.transform)
                        spent.Add(child.gameObject);
                    foreach (GameObject child in spent)
                        Object.DestroyImmediate(child);
                }
            }
        }

        /// <summary>The ground-plane rectangle of every wall collider built so far.</summary>
        private static List<Rect> BuiltWallFootprints(Transform parent)
        {
            var footprints = new List<Rect>();
            foreach (BoxCollider collider in parent.GetComponentsInChildren<BoxCollider>())
            {
                if (!collider.name.StartsWith("DungeonWall"))
                    continue;
                Bounds bounds = collider.bounds;
                footprints.Add(
                    Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z)
                );
            }
            return footprints;
        }

        private static bool Matches(Rect actual, Rect plan)
        {
            return Mathf.Abs(actual.xMin - plan.xMin) <= Tolerance
                && Mathf.Abs(actual.xMax - plan.xMax) <= Tolerance
                && Mathf.Abs(actual.yMin - plan.yMin) <= Tolerance
                && Mathf.Abs(actual.yMax - plan.yMax) <= Tolerance;
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
