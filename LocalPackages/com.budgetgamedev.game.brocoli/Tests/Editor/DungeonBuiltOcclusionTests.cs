using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Ties the wall-visibility decision to what actually gets built. The property
    /// tests reason about planned geometry and grouping rules; this builds the real
    /// prefabs and checks the scene agrees - the slab is the height the sight-line
    /// maths assumes, and touching interior runs really do end up in one occlusion
    /// section rather than only in the planner's idea of one.
    /// </summary>
    public sealed class DungeonBuiltOcclusionTests
    {
        private const string WallPrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonWall.prefab";

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

        /// <summary>
        /// Whether a wall hides anyone depends on how tall it is, so the constant
        /// the occlusion maths uses has to be the height the prefab builds.
        /// </summary>
        [Test]
        public void BuiltWallSlabsStandAtThePlannedHeight()
        {
            DungeonRoomBuilder builder = Builder();
            builder.BuildEdge(
                root.transform,
                new DungeonEdge(0, 0, true),
                new DungeonPassage(false, 0, 0)
            );

            // Whatever the run built is what gets measured. Matching on a prefab
            // name here would stop covering the wall the moment the art was
            // swapped, and would keep passing while it did.
            var heights = new List<float>();
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (!collider.isTrigger)
                    heights.Add(collider.bounds.size.y);
            }

            Assert.That(heights, Is.Not.Empty, "the edge built no wall colliders");
            foreach (float height in heights)
            {
                Assert.That(
                    height,
                    Is.EqualTo(DungeonWallPiece.SlabHeight).Within(0.02f),
                    $"a built wall slab stands {height:0.00} tall, but the occlusion maths "
                        + $"assumes {DungeonWallPiece.SlabHeight:0.00}"
                );
            }
        }

        /// <summary>
        /// A freestanding structure is one occlusion section in the scene, so a
        /// cross lowers every arm at once instead of dropping only the run the
        /// camera happened to hit.
        /// </summary>
        [Test]
        public void TouchingInteriorRunsAreBuiltIntoOneOcclusionSection()
        {
            Assert.That(
                WallVisibilityFixtures.TryFindInteriorStructure(out int seed, out Vector2Int room),
                "no generated room in the corpus builds crossing interior runs"
            );

            var layout = new DungeonLayout(seed);
            var planned = new List<DungeonWallPiece>();
            DungeonRoomGeometry.AppendInteriorWalls(planned, room, layout.Archetype(room));
            Builder().BuildInterior(root.transform, room, layout.Archetype(room));

            var sections = new Dictionary<Vector2, DungeonOcclusionSection>();
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger)
                    continue;
                Bounds bounds = collider.bounds;
                sections[new Vector2(bounds.center.x, bounds.center.z)] =
                    DungeonOccluder.Owning(collider) as DungeonOcclusionSection;
            }

            Assert.That(
                sections,
                Is.Not.Empty,
                $"seed {seed}: room {room} built no interior walls"
            );
            int crossings = 0;
            for (int i = 0; i < planned.Count; i++)
            for (int j = i + 1; j < planned.Count; j++)
            {
                if (
                    planned[i].AlongX == planned[j].AlongX
                    || !DungeonWallGrouping.AreInContact(planned[i], planned[j])
                )
                    continue;

                crossings++;
                Assert.That(
                    SectionAt(sections, planned[j].Anchor),
                    Is.SameAs(SectionAt(sections, planned[i].Anchor)),
                    $"seed {seed}: room {room} built {planned[i]} and the run crossing it at "
                        + $"{planned[j]} into separate occlusion sections, so one would lower "
                        + "while the other stayed standing"
                );
            }

            Assert.That(
                crossings,
                Is.GreaterThan(0),
                $"seed {seed}: room {room} built no crossing"
            );
        }

        private DungeonRoomBuilder Builder()
        {
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
            Assert.That(wallPrefab, Is.Not.Null, WallPrefabPath);

            DungeonRoomBuilder builder = host.AddComponent<DungeonRoomBuilder>();
            var serialized = new SerializedObject(builder);
            serialized.FindProperty("wallPrefab").objectReferenceValue = wallPrefab;
            serialized.ApplyModifiedProperties();
            return builder;
        }

        private static DungeonOcclusionSection SectionAt(
            Dictionary<Vector2, DungeonOcclusionSection> sections,
            Vector2 anchor
        )
        {
            foreach (KeyValuePair<Vector2, DungeonOcclusionSection> section in sections)
            {
                if (Vector2.Distance(section.Key, anchor) <= 0.05f)
                    return section.Value;
            }
            return null;
        }
    }
}
