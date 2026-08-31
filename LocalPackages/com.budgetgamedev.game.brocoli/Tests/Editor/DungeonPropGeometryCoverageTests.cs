using System;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonPropGeometryCoverageTests
    {
        [Test]
        public void EveryColliderShapeContributesToPrefabMeasurement()
        {
            GameObject root = new("Coverage Prop Measurement");
            Mesh mesh = new();
            mesh.vertices = new[] { Vector3.zero, Vector3.one, Vector3.right * 2f };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            try
            {
                GameObject emptyMesh = Child(root, "Empty Mesh");
                emptyMesh.AddComponent<MeshFilter>();
                GameObject sphereObject = Child(root, "Sphere");
                sphereObject.AddComponent<SphereCollider>().radius = 2f;
                GameObject capsuleObject = Child(root, "Capsule");
                CapsuleCollider capsule = capsuleObject.AddComponent<CapsuleCollider>();
                capsule.radius = 1f;
                capsule.height = 4f;
                foreach (int direction in new[] { -1, 0, 1, 2, 3 })
                {
                    capsule.direction = Mathf.Clamp(direction, 0, 2);
                    Assert.That(DungeonPropMeasurement.Of(root).Radius, Is.GreaterThan(0f));
                }
                GameObject meshObject = Child(root, "Mesh Collider");
                meshObject.AddComponent<MeshCollider>().sharedMesh = mesh;
                Assert.That(DungeonPropMeasurement.Of(root).Height, Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ChestWallClearanceCoversEveryRoomShapeAndDividerOrientation()
        {
            foreach (
                DungeonLayout.RoomShape shape in Enum.GetValues(typeof(DungeonLayout.RoomShape))
            )
            {
                foreach (int variant in new[] { 0, 1 })
                {
                    var room = new DungeonLayout.RoomArchetype(
                        shape,
                        DungeonLayout.RoomTheme.Empty,
                        12f,
                        8f,
                        variant
                    );
                    foreach (
                        Vector2 point in new[]
                        {
                            Vector2.zero,
                            new Vector2(0f, 4f),
                            new Vector2(4f, 0f),
                            new Vector2(8f, 0f),
                            new Vector2(0f, 8f),
                            new Vector2(20f, 20f),
                        }
                    )
                        DungeonPropPlacer.OverlapsInteriorWall(point, 0.5f, room);
                }
            }
        }

        [Test]
        public void PropResolutionSkipsNullEntriesAndReportsNoMatch()
        {
            GameObject named = new("Known-Prop");
            try
            {
                Assert.That(
                    DungeonPropPlacer.ResolveProp(new GameObject[] { null, named }, "knownprop"),
                    Is.SameAs(named)
                );
                Assert.That(
                    DungeonPropPlacer.ResolveProp(new GameObject[] { null, named }, "missing"),
                    Is.Null
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(named);
            }
        }

        private static GameObject Child(GameObject root, string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(root.transform, false);
            return child;
        }
    }
}
