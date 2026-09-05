using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class BotGeometryIntegrationTests
    {
        [TestCase(1675487028, -6, 3, -160.571f, 69.306f)]
        [TestCase(924672326, -1, 2, -40.941f, 49.286f)]
        public void RecordedWallPinAllowsLegalTangentsOutsideTheConservativeNavMeshInset(
            int seed,
            int roomX,
            int roomZ,
            float playerX,
            float playerZ
        )
        {
            Setup(false);
            BuildAuthoredRoom(seed, new Vector2Int(roomX, roomZ));
            var capsule = player.GetComponent<CapsuleCollider>();
            capsule.radius = 0.43f;
            capsule.height = 1f;
            capsule.center = Vector3.up * 0.5f;
            player.transform.position = new Vector3(playerX, 0f, playerZ);
            // The native trace records distance but not enemy angle. This explicit
            // synthetic south blocker tests the plausible remaining escape geometry.
            var enemy = new GameObject("Synthetic south enemy stand-off", typeof(CapsuleCollider));
            objects.Add(enemy);
            enemy.layer = LayerMask.NameToLayer("Enemy");
            enemy.transform.position = player.transform.position + Vector3.back * 0.994f;
            var enemyCapsule = enemy.GetComponent<CapsuleCollider>();
            enemyCapsule.radius = 0.43f;
            enemyCapsule.height = 1f;
            enemyCapsule.center = Vector3.up * 0.5f;
            Physics.SyncTransforms();
            NavMesh.SamplePosition(
                player.transform.position,
                out NavMeshHit nearest,
                1.5f,
                NavMesh.AllAreas
            );
            Assert.That(
                Vector2.Distance(nearest.position.ToGround(), player.transform.position.ToGround()),
                Is.GreaterThan(0.25f)
            );
            Assert.That(
                movement.PreviewNavigationDelta(Vector2.up * 0.033333f).magnitude,
                Is.LessThan(0.005f)
            );
            Assert.That(
                movement.PreviewNavigationDelta(Vector2.down * 0.033333f).magnitude,
                Is.LessThan(0.005f)
            );
            Assert.That(
                movement.PreviewNavigationDelta(Vector2.right * 0.033333f).magnitude,
                Is.GreaterThan(0.03f)
            );
            float initialX = player.transform.position.x;
            // Stop once the blocker is escaped; continually steering north after
            // that would patrol the solid wall and can return to the starting x.
            for (
                int frame = 0;
                frame < 90 && Mathf.Abs(player.transform.position.x - initialX) <= 0.5f;
                frame++
            )
            {
                Vector2 input = (Vector2)
                    typeof(BotDriver)
                        .GetMethod("NavigateLocal", Hidden)
                        .Invoke(
                            bot,
                            new object[] { player.transform.position.ToGround(), Vector2.up }
                        );
                Step(input);
            }
            Assert.That(
                Mathf.Abs(player.transform.position.x - initialX),
                Is.GreaterThan(0.5f),
                "physical wall tangents must remain available when moving inward is blocked"
            );
            Assert.That(
                player.transform.position.z,
                Is.LessThanOrEqualTo(playerZ + 0.001f),
                "the wall remains solid"
            );
        }

        [Test]
        public void RealRoomPropNavMeshSeamCannotVetoPhysicallyClearRecoverySteps()
        {
            Setup(false);
            BuildAuthoredRoom(1929387038, new Vector2Int(-8, -3));
            var capsule = player.GetComponent<CapsuleCollider>();
            capsule.radius = 0.43f;
            capsule.height = 1f;
            capsule.center = Vector3.up * 0.5f;
            player.transform.position = new Vector3(-222.124f, 0f, -63.929f);
            Physics.SyncTransforms();
            Vector2 initial = player.transform.position.ToGround();
            NavMesh.SamplePosition(
                player.transform.position,
                out NavMeshHit from,
                1.5f,
                NavMesh.AllAreas
            );
            NavMesh.SamplePosition(
                (initial + Vector2.up * 0.033333f).ToWorld(from.position.y),
                out NavMeshHit to,
                1.5f,
                NavMesh.AllAreas
            );
            Assert.That(
                NavMesh.Raycast(from.position, to.position, out _, NavMesh.AllAreas),
                Is.True,
                "fixture reproduces the recorded zero-distance baked prop seam veto"
            );
            Assert.That(
                movement.PreviewNavigationDelta(Vector2.up * 0.033333f).magnitude,
                Is.GreaterThan(0.03f),
                "the actual capsule has a clear step despite the mesh seam"
            );
            Vector2 target = new(-224f, -60f);
            for (
                int frame = 0;
                frame < 160
                    && Vector2.Distance(player.transform.position.ToGround(), target) > 0.3f;
                frame++
            )
            {
                Vector2 position = player.transform.position.ToGround();
                Vector2 input = (Vector2)
                    typeof(BotDriver)
                        .GetMethod("NavigateLocal", Hidden)
                        .Invoke(bot, new object[] { position, target - position });
                Step(input);
            }
            Assert.That(
                Vector2.Distance(player.transform.position.ToGround(), initial),
                Is.GreaterThan(1f)
            );
            Assert.That(
                Vector2.Distance(player.transform.position.ToGround(), target),
                Is.LessThan(0.4f)
            );
        }

        private void BuildAuthoredRoom(int seed, Vector2Int center)
        {
            mesh.Remove();
            UnityEngine.Object.DestroyImmediate(data);
            data = null;
            var root = new GameObject("Authored room collision regression");
            objects.Add(root);
            var scene = EditorSceneManager.OpenScene(
                "Packages/com.budgetgamedev.game.brocoli/Scenes/Brocoli_Dungeon_Common.unity",
                OpenSceneMode.Additive
            );
            try
            {
                DungeonRoomBuilder builder = null;
                DungeonPropPlacer decor = null;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                {
                    if (builder == null)
                        builder = sceneRoot.GetComponentInChildren<DungeonRoomBuilder>(true);
                    if (decor == null)
                        decor = sceneRoot.GetComponentInChildren<DungeonPropPlacer>(true);
                }
                var layout = new DungeonLayout(seed);
                for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    var room = center + new Vector2Int(x, z);
                    if (!layout.IsPlayableRoom(room))
                        continue;
                    var archetype = layout.Archetype(room);
                    builder.BuildFloor(
                        root.transform,
                        room,
                        archetype,
                        layout.RoomRandom(room, 404)
                    );
                    builder.BuildInterior(root.transform, room, archetype);
                    decor.BuildContents(
                        root.transform,
                        room,
                        archetype,
                        layout.RoomRandom(room, 505),
                        new HashSet<int>()
                    );
                    decor.BuildAtmosphere(
                        root.transform,
                        room,
                        archetype,
                        layout.PlayableDoorways(room),
                        layout.RoomRandom(room, 707),
                        layout.ShellWallMask(room)
                    );
                }
                for (int direction = 0; direction < 4; direction++)
                {
                    var edge = DungeonLayout.EdgeBetween(center, direction);
                    builder.BuildEdge(
                        root.transform,
                        edge,
                        layout.Passage(edge, layout.IsDoorOpen(center, direction))
                    );
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.layerMask = ~(1 << 2);
            surface.BuildNavMesh();
        }
    }
}
