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
    /// maths assumes, and low interior plans stay low while building the real
    /// objects in either orientation.
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
        /// Interior dividers retain wall collision and grouping, but are always
        /// built waist-high so they cannot become opaque walls through playable
        /// floor.
        /// </summary>
        [TestCase(DungeonLayout.RoomShape.NarrowVertical)]
        [TestCase(DungeonLayout.RoomShape.NarrowHorizontal)]
        public void BuiltInteriorsAreLowOcclusionSections(DungeonLayout.RoomShape shape)
        {
            var room = Vector2Int.zero;
            var archetype = new DungeonLayout.RoomArchetype(
                shape,
                DungeonLayout.RoomTheme.Sparse,
                10.2f,
                8.2f,
                0
            );
            var planned = new List<DungeonWallPiece>();
            DungeonRoomGeometry.AppendInteriorWalls(planned, room, archetype);
            Builder().BuildInterior(root.transform, room, archetype);
            Physics.SyncTransforms();

            var sections = new Dictionary<Vector2, DungeonOcclusionSection>();
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger)
                    continue;
                Bounds bounds = collider.bounds;
                sections[new Vector2(bounds.center.x, bounds.center.z)] =
                    DungeonOccluder.Owning(collider) as DungeonOcclusionSection;
            }

            Assert.That(sections, Is.Not.Empty, "the interior route built no wall colliders");
            foreach (DungeonWallPiece piece in planned)
            {
                DungeonOcclusionSection section = SectionAt(sections, piece.Anchor);
                Assert.That(section, Is.Not.Null, $"built slab at {piece.Anchor} was not enrolled");

                Collider collider = section.GetComponentInChildren<Collider>();
                Assert.That(collider, Is.Not.Null, $"built slab at {piece.Anchor} has no collider");
                Assert.That(
                    collider.bounds.size.y,
                    Is.EqualTo(
                            DungeonWallPiece.SlabHeight
                                * (
                                    piece.AlongX
                                        ? DungeonRoomBuilder.InteriorRailingHeightScale
                                        : DungeonRoomBuilder.InteriorWallHeightScale
                                )
                        )
                        .Within(0.02f),
                    $"interior slab at {piece.Anchor} was built as a full-height wall"
                );
                Assert.That(
                    collider.bounds.size.y,
                    Is.LessThan(DungeonOccluder.MinimumAutomaticFadeHeight),
                    $"interior slab at {piece.Anchor} is tall enough to hide a character"
                );
            }
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
