using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The imported wall model includes its own floor tile and loose rocks, but
    /// the dungeon's wall prefabs use an apron-free derivative. These tests pin
    /// the compact visible depth on every edge style so the source geometry
    /// cannot return unnoticed.
    /// </summary>
    public sealed class DungeonBoundaryOverhangTests
    {
        private const float Tolerance = 0.05f;

        private static readonly float ApronFreeDepth =
            DungeonWallPiece.MeshDepthAlongNormal + DungeonWallPiece.MeshDepthAgainstNormal;

        private GameObject host;
        private GameObject root;
        private DungeonRoomBuilder builder;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Overhang host");
            root = new GameObject("Overhang root");
            builder = DungeonPropFixtures.Builder(host);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(host);
        }

        /// <summary>
        /// The camera-facing facade still projects slightly beyond the collision
        /// line because its stones have visible depth, but never by the source
        /// floor tile's broad apron.
        /// </summary>
        [Test]
        public void TheCliffFacadeProjectsPastItsCollisionLine()
        {
            var edge = new DungeonEdge(0, 0, true);
            GameObject built = Build(edge, DungeonEdgeStyle.SouthCliff);

            Assert.That(
                ReachPastTheLip(built, edge),
                Is.GreaterThan(0f),
                "the cliff model no longer overhangs, so this whole rule is stale"
            );
        }

        /// <summary>
        /// The camera-facing cliff: every piece stops at the edge of the tile
        /// it stands on. A piece reaching further is carrying loose rocks out
        /// over the void.
        /// </summary>
        [Test]
        public void TheSouthCliffHangsNothingLooseOverTheVoid()
        {
            GameObject built = Build(new DungeonEdge(0, 0, true), DungeonEdgeStyle.SouthCliff);

            Assert.That(
                DeepestPiece(built, Vector3.back),
                Is.EqualTo(DungeonWallPiece.SlabThickness).Within(Tolerance),
                "a south cliff piece still projects past the structural wall"
            );
        }

        /// <summary>The same drop, where the yawed camera sees the side steps.</summary>
        [Test]
        public void TheSideCliffHangsNothingLooseOverTheVoid()
        {
            GameObject built = Build(new DungeonEdge(0, 0, false), DungeonEdgeStyle.SideCliff);

            Assert.That(
                DeepestPiece(built, Vector3.left),
                Is.EqualTo(DungeonWallPiece.SlabThickness).Within(Tolerance),
                "a side cliff piece still projects past the structural wall"
            );
        }

        /// <summary>
        /// The cliff facade is one parapet and two copies of the wall stacked
        /// vertically. Their dedicated mesh stops at the structural slab instead
        /// of repeating the source model's broad base apron at any seam.
        /// </summary>
        [Test]
        public void TheStackedCliffCoursesHaveNoBaseLedge()
        {
            GameObject built = Build(new DungeonEdge(0, 0, true), DungeonEdgeStyle.SouthCliff);
            Transform parapet = built.transform.Find("Low Dungeon Railing");
            Transform shell = built.transform.Find("Cliff Face Below Floor");

            Assert.That(parapet, Is.Not.Null, "the cliff parapet was not built");
            Assert.That(shell, Is.Not.Null, "the cliff shell was not built");
            Assert.That(
                DeepestPiece(shell.gameObject, Vector3.back),
                Is.EqualTo(DungeonWallPiece.SlabThickness).Within(Tolerance),
                "a stacked shell course still projects past the structural wall"
            );
            Assert.That(
                DeepestPiece(parapet.gameObject, Vector3.back),
                Is.EqualTo(DungeonWallPiece.SlabThickness).Within(Tolerance),
                "the top parapet still projects an apron ledge over the cliff"
            );
            AssertThatAllPiecesShareOneFace(built, Vector3.back);
        }

        /// <summary>
        /// A solid boundary is the same facade as the camera-facing cliff, so it
        /// exposes the same flat structural face rather than a source floor apron.
        /// The platform ends there too, whichever way the edge happens to look.
        /// </summary>
        [Test]
        public void ASolidBoundaryHasNoSourceFloorApron()
        {
            GameObject built = Build(new DungeonEdge(0, 0, true), DungeonEdgeStyle.SolidBoundary);

            Assert.That(
                DeepestPiece(built, Vector3.back),
                Is.EqualTo(DungeonWallPiece.SlabThickness).Within(Tolerance),
                "the boundary still carries the source floor apron"
            );
            AssertThatAllPiecesShareOneFace(built, Vector3.back);
        }

        /// <summary>
        /// An ordinary shared run also exposes only the masonry slab, allowing
        /// the authored dungeon floor to continue cleanly on both sides.
        /// </summary>
        [Test]
        public void ASharedRunHasNoSourceFloorApron()
        {
            GameObject built = Build(new DungeonEdge(0, 0, true), DungeonEdgeStyle.Interior);

            Assert.That(
                DeepestPiece(built, Vector3.back),
                Is.EqualTo(ApronFreeDepth).Within(Tolerance),
                "a shared run still carries the source floor apron"
            );
        }

        private GameObject Build(DungeonEdge edge, DungeonEdgeStyle style)
        {
            return builder.BuildEdge(
                root.transform,
                edge,
                new DungeonPassage(false, 0, 0),
                style,
                DungeonLayout.EnvironmentTheme.Dungeon
            );
        }

        /// <summary>
        /// The deepest any one built piece measures along the axis its apron
        /// points down. Measured piece by piece as a span rather than against
        /// the edge line, so it says the same thing about a cliff course that
        /// was deliberately stepped further out. Read off the drawn vertices
        /// rather than off bounds: a trimmed mesh keeps the vertices it no
        /// longer draws.
        /// </summary>
        private static float DeepestPiece(GameObject built, Vector3 apron)
        {
            float deepest = float.MinValue;
            foreach (MeshFilter filter in built.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;

                Vector3[] vertices = filter.sharedMesh.vertices;
                float near = float.MaxValue;
                float far = float.MinValue;
                foreach (int index in filter.sharedMesh.triangles)
                {
                    float along = Vector3.Dot(
                        filter.transform.TransformPoint(vertices[index]),
                        apron
                    );
                    near = Mathf.Min(near, along);
                    far = Mathf.Max(far, along);
                }
                deepest = Mathf.Max(deepest, far - near);
            }

            Assert.That(deepest, Is.GreaterThan(float.MinValue), "nothing was built to measure");
            return deepest;
        }

        private static void AssertThatAllPiecesShareOneFace(GameObject built, Vector3 apron)
        {
            float expectedNear = 0f;
            float expectedFar = 0f;
            bool foundFirst = false;
            foreach (MeshFilter filter in built.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;

                Vector3[] vertices = filter.sharedMesh.vertices;
                float near = float.MaxValue;
                float far = float.MinValue;
                foreach (int index in filter.sharedMesh.triangles)
                {
                    float along = Vector3.Dot(
                        filter.transform.TransformPoint(vertices[index]),
                        apron
                    );
                    near = Mathf.Min(near, along);
                    far = Mathf.Max(far, along);
                }

                if (!foundFirst)
                {
                    expectedNear = near;
                    expectedFar = far;
                    foundFirst = true;
                    continue;
                }

                Assert.That(
                    near,
                    Is.EqualTo(expectedNear).Within(Tolerance),
                    $"{filter.name} starts on a different cliff plane"
                );
                Assert.That(
                    far,
                    Is.EqualTo(expectedFar).Within(Tolerance),
                    $"{filter.name} ends on a different cliff plane"
                );
            }

            Assert.That(foundFirst, Is.True, "nothing was built to align");
        }

        /// <summary>How far past the edge line the built masonry hangs.</summary>
        private static float ReachPastTheLip(GameObject built, DungeonEdge edge)
        {
            Vector3 apron = edge.Horizontal ? Vector3.back : Vector3.left;
            float lip = edge.Horizontal
                ? (edge.Y + 0.5f) * DungeonLayout.RoomDepth
                : (edge.X + 0.5f) * DungeonLayout.RoomWidth;

            float furthest = float.MinValue;
            foreach (MeshFilter filter in built.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;

                Vector3[] vertices = filter.sharedMesh.vertices;
                foreach (int index in filter.sharedMesh.triangles)
                {
                    furthest = Mathf.Max(
                        furthest,
                        Vector3.Dot(filter.transform.TransformPoint(vertices[index]), apron)
                    );
                }
            }

            Assert.That(furthest, Is.GreaterThan(float.MinValue), "nothing was built to measure");
            return furthest + lip;
        }
    }
}
