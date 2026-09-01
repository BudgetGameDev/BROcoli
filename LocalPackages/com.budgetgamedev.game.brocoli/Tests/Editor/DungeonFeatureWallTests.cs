using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The full-height interior feature wall lives under one contract: it may
    /// stand tall only because the band its occlusion shadow falls on is sealed
    /// - by collision, and by the prop placer refusing to put anything
    /// reachable there. These tests pin both halves of that contract.
    /// </summary>
    public sealed class DungeonFeatureWallTests
    {
        private const string WallPrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonWall.prefab";

        private static DungeonLayout.RoomArchetype Eligible =>
            new(
                DungeonLayout.RoomShape.OpenHall,
                DungeonLayout.RoomTheme.Storage,
                10.2f,
                6.4f,
                1
            );

        /// <summary>
        /// The keep-out band starts at the wall's back face and covers the whole
        /// sheared shadow the 45-degree camera casts past its north-east side:
        /// deep enough for the 42-degree pitch, and reaching east past the
        /// wall's end so the diagonal view cannot peek a hidden pocket.
        /// </summary>
        [Test]
        public void KeepOutCoversTheWallsOcclusionShadow()
        {
            var walls = new List<DungeonWallPiece>();
            DungeonRoomGeometry.AppendInteriorWalls(walls, Vector2Int.zero, Eligible);
            var features = walls.FindAll(w => w.Kind == DungeonWallKind.InteriorFeature);
            Assert.That(features, Is.Not.Empty, "eligible archetype planned no feature wall");

            var keepOuts = new List<Rect>();
            DungeonRoomGeometry.AppendFeatureKeepOuts(keepOuts, Vector2Int.zero, Eligible);
            Assert.That(keepOuts.Count, Is.EqualTo(1));
            Rect keepOut = keepOuts[0];

            // The camera looks north-east at 42 degrees of pitch, so a wall of
            // slab height hides a ground band this long, sheared equally into
            // +x and +z.
            float shadow =
                DungeonWallPiece.SlabHeight
                / Mathf.Tan(42f * Mathf.Deg2Rad)
                * Mathf.Cos(45f * Mathf.Deg2Rad);

            float wallMinX = float.MaxValue;
            float wallMaxX = float.MinValue;
            float backFace = float.MinValue;
            foreach (DungeonWallPiece piece in features)
            {
                Rect footprint = piece.Footprint;
                wallMinX = Mathf.Min(wallMinX, footprint.xMin);
                wallMaxX = Mathf.Max(wallMaxX, footprint.xMax);
                backFace = Mathf.Max(backFace, footprint.yMax);
            }

            Assert.That(keepOut.yMin, Is.LessThanOrEqualTo(backFace + 0.001f));
            Assert.That(keepOut.yMax, Is.GreaterThanOrEqualTo(backFace + shadow - 0.001f));
            Assert.That(keepOut.xMin, Is.LessThanOrEqualTo(wallMinX + 0.001f));
            Assert.That(keepOut.xMax, Is.GreaterThanOrEqualTo(wallMaxX + shadow - 0.001f));

            // And the whole band stays inside the interior envelope, so sealing
            // it can never block the perimeter corridor.
            Assert.That(
                Mathf.Abs(keepOut.xMin),
                Is.LessThanOrEqualTo(DungeonRoomGeometry.InteriorHalfWidthLimit)
            );
            Assert.That(
                Mathf.Abs(keepOut.xMax),
                Is.LessThanOrEqualTo(DungeonRoomGeometry.InteriorHalfWidthLimit)
            );
            Assert.That(
                keepOut.yMax,
                Is.LessThanOrEqualTo(DungeonRoomGeometry.InteriorHalfDepthLimit)
            );
        }

        /// <summary>Feature walls stay rare across real generated rooms.</summary>
        [Test]
        public void FeatureWallsAreSparse()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                int featureRooms = 0;
                int rooms = 0;
                for (int x = -30; x <= 30; x++)
                for (int y = -2; y <= 2; y++)
                {
                    rooms++;
                    if (DungeonRoomGeometry.HasFeatureWall(layout.Archetype(new Vector2Int(x, y))))
                        featureRooms++;
                }
                Assert.That(
                    featureRooms,
                    Is.LessThan(rooms / 10),
                    $"seed {seed}: feature walls are not sparse"
                );
            }
        }

        /// <summary>
        /// Prop and chest placement treats the sealed band as solid: nothing
        /// reachable is ever asked to stand where the player cannot go.
        /// </summary>
        [Test]
        public void PlacementTreatsTheKeepOutAsSolid()
        {
            var keepOuts = new List<Rect>();
            DungeonRoomGeometry.AppendFeatureKeepOuts(keepOuts, Vector2Int.zero, Eligible);
            Rect keepOut = keepOuts[0];
            Assert.That(
                DungeonPropPlacer.OverlapsInteriorWall(keepOut.center, 0.4f, Eligible),
                Is.True
            );
            Assert.That(
                DungeonPropPlacer.OverlapsInteriorWall(
                    new Vector2(keepOut.xMax - 0.2f, keepOut.yMax - 0.2f),
                    0.4f,
                    Eligible
                ),
                Is.True
            );
        }

        /// <summary>
        /// Built for real, the feature pieces stand at full slab height while
        /// every other interior piece stays low, and the keep-out band is
        /// physically sealed by a wall-layer collider covering its rectangle.
        /// </summary>
        [Test]
        public void BuiltFeatureWallIsTallAndItsBandIsSealed()
        {
            var host = new GameObject("BuilderHost");
            var root = new GameObject("BuiltRoom");
            try
            {
                var builder = host.AddComponent<DungeonRoomBuilder>();
                var serialized = new SerializedObject(builder);
                serialized.FindProperty("wallPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
                serialized.ApplyModifiedProperties();

                builder.BuildInterior(root.transform, Vector2Int.zero, Eligible);
                Physics.SyncTransforms();

                bool sawFeature = false;
                foreach (Transform child in root.GetComponentsInChildren<Transform>())
                {
                    if (child.name != "DungeonWall - Interior Feature Wall")
                        continue;
                    sawFeature = true;
                    Collider collider = child.GetComponentInChildren<Collider>();
                    Assert.That(collider, Is.Not.Null);
                    Assert.That(
                        collider.bounds.size.y,
                        Is.EqualTo(DungeonWallPiece.SlabHeight).Within(0.02f),
                        "feature wall is not full height"
                    );
                }
                Assert.That(sawFeature, Is.True, "no feature wall was built");

                var keepOuts = new List<Rect>();
                DungeonRoomGeometry.AppendFeatureKeepOuts(keepOuts, Vector2Int.zero, Eligible);
                Rect keepOut = keepOuts[0];
                Transform blocker = null;
                foreach (Transform child in root.GetComponentsInChildren<Transform>())
                {
                    if (child.name == "Feature Wall Keep-Out Collision")
                        blocker = child;
                }
                Assert.That(blocker, Is.Not.Null, "keep-out band has no collider");
                var box = blocker.GetComponent<BoxCollider>();
                Assert.That(box.bounds.min.x, Is.LessThanOrEqualTo(keepOut.xMin + 0.01f));
                Assert.That(box.bounds.max.x, Is.GreaterThanOrEqualTo(keepOut.xMax - 0.01f));
                Assert.That(box.bounds.min.z, Is.LessThanOrEqualTo(keepOut.yMin + 0.01f));
                Assert.That(box.bounds.max.z, Is.GreaterThanOrEqualTo(keepOut.yMax - 0.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(host);
            }
        }
    }
}
