using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class BotGeometryIntegrationTests
    {
        [Test]
        public void AuthoredDungeonFloorHeightAllowsMovementFromTheRecordedStallPosition()
        {
            Setup(false);
            mesh.Remove();
            Object.DestroyImmediate(data);
            data = null;
            var root = new GameObject("Actual dungeon floor navigation regression");
            objects.Add(root);
            var builder = root.AddComponent<DungeonRoomBuilder>();
            var serialized = new SerializedObject(builder);
            serialized.FindProperty("floorPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonFloor.prefab"
                );
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var layout = new DungeonLayout(692788717);
            var room = new Vector2Int(100, 101);
            builder.BuildFloor(root.transform, room, layout.Archetype(room), new System.Random(1));
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.layerMask = ~(1 << 2);
            surface.BuildNavMesh();

            // Recorded seed12345 position, translated to the isolated fixture room.
            SetPosition(new Vector2(6.476f, 11.974f));
            Vector2 destination = Origin.ToGround() + new Vector2(0, 20);
            Assert.That(
                NavMesh.SamplePosition(
                    player.transform.position,
                    out NavMeshHit floor,
                    2f,
                    NavMesh.AllAreas
                ),
                Is.True
            );
            Assert.That(
                floor.position.y,
                Is.GreaterThan(0.08f),
                "authored render-mesh floor differs from the old y=0 synthetic fixture"
            );
            for (
                int frame = 0;
                frame < 400
                    && Vector2.Distance(player.transform.position.ToGround(), destination) > 0.25f;
                frame++
            )
            {
                Vector2 input = (Vector2)
                    typeof(BotDriver)
                        .GetMethod("NavigateTo", Hidden)
                        .Invoke(
                            bot,
                            new object[] { player.transform.position.ToGround(), destination }
                        );
                Step(input);
            }
            Assert.That(
                Vector2.Distance(player.transform.position.ToGround(), destination),
                Is.LessThan(0.3f)
            );
            Assert.That(BotDriver.MovementBlocked, Is.False);
        }

        [Test]
        public void SafeInwardStepsCanReenterTheNavMeshOverSeveralPhysicsFrames()
        {
            Setup(false);
            SetPosition(new Vector2(9.8f, 0));
            Vector2 start = player.transform.position.ToGround();
            Assert.That(
                NavMesh.SamplePosition(
                    player.transform.position,
                    out NavMeshHit floor,
                    2f,
                    NavMesh.AllAreas
                ),
                Is.True
            );
            float initialOffset = Vector2.Distance(start, floor.position.ToGround());
            Assert.That(initialOffset, Is.GreaterThan(0.1f));
            Assert.That(
                BotDriver.AcceptsHorizontalProjection(initialOffset, initialOffset + 0.03f),
                Is.False
            );
            for (int frame = 0; frame < 20; frame++)
            {
                Vector2 input = (Vector2)
                    typeof(BotDriver)
                        .GetMethod("NavigateLocal", Hidden)
                        .Invoke(
                            bot,
                            new object[] { player.transform.position.ToGround(), Vector2.left }
                        );
                Assert.That(
                    input.x,
                    Is.LessThan(0),
                    "safe reentry must not require reaching the mesh in one tick"
                );
                Step(input);
            }
            NavMesh.SamplePosition(player.transform.position, out floor, 2f, NavMesh.AllAreas);
            Assert.That(
                Vector2.Distance(player.transform.position.ToGround(), floor.position.ToGround()),
                Is.LessThan(0.05f)
            );
        }

        [Test]
        public void RejectedMovementStartsRecoveryEvenWhenAcceptedInputIsZero()
        {
            Setup(false);
            SetPosition(Vector2.zero);
            mesh.Remove();
            Vector2 position = player.transform.position.ToGround();
            Vector2 input = (Vector2)
                typeof(BotDriver)
                    .GetMethod("NavigateLocal", Hidden)
                    .Invoke(bot, new object[] { position, Vector2.right });
            Assert.That(input, Is.EqualTo(Vector2.zero));
            Assert.That(BotDriver.MovementBlocked, Is.True);
            Set(bot, "hasPosition", true);
            Set(bot, "lastPosition", position);
            Set(bot, "lastProgressPosition", position);
            Set(bot, "stationaryTime", 2f);
            Set(bot, "nextProgressCheck", Time.time - 1f);
            int before = BotDriver.StuckRecoveryCount;
            typeof(BotDriver)
                .GetMethod("TrackProgress", Hidden)
                .Invoke(bot, new object[] { position });
            Assert.That(BotDriver.StuckRecoveryCount, Is.GreaterThan(before));
        }
    }
}
