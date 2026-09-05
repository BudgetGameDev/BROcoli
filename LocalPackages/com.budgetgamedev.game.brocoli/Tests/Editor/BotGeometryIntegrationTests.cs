using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class BotGeometryIntegrationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 Origin = new(2800, 0, 2000);
        private readonly List<GameObject> objects = new();
        private NavMeshData data;
        private NavMeshDataInstance mesh;
        private GameObject player;
        private PlayerMovement movement;
        private BotDriver bot;

        [TearDown]
        public void Cleanup()
        {
            mesh.Remove();
            if (data != null)
                Object.DestroyImmediate(data);
            foreach (GameObject host in objects)
                Object.DestroyImmediate(host);
            objects.Clear();
        }

        [Test]
        public void SmallLocalGoalChangesKeepAStablePhysicallyValidHeading()
        {
            Setup(false);
            SetPosition(Vector2.zero);
            Vector2 previous = Vector2.zero;
            for (int frame = 0; frame < 30; frame++)
            {
                Vector2 goal = new(1f, frame % 2 == 0 ? 0.01f : -0.01f);
                Vector2 input = (Vector2)
                    typeof(BotDriver)
                        .GetMethod("NavigateLocal", Hidden)
                        .Invoke(bot, new object[] { player.transform.position.ToGround(), goal });
                if (frame > 0)
                    Assert.That(Vector2.Distance(input, previous), Is.LessThan(0.001f));
                previous = input;
                Step(input);
            }
            Assert.That(player.transform.position.x - Origin.x, Is.GreaterThan(0.5f));
        }

        [Test]
        public void CompleteRouteFollowsRealNavMeshCornersAroundASolidDivider()
        {
            Setup(true);
            SetPosition(new Vector2(-4, 0));
            Vector2 destination = Origin.ToGround() + Vector2.right * 4f;
            var path = new NavMeshPath();
            Assert.That(
                NavMesh.CalculatePath(
                    player.transform.position,
                    destination.ToWorld(),
                    NavMesh.AllAreas,
                    path
                ),
                Is.True
            );
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(
                path.corners.Length,
                Is.GreaterThan(2),
                "fixture must require turning around the physical divider"
            );
            float greatestDetour = 0;
            for (
                int frame = 0;
                frame < 800
                    && Vector2.Distance(player.transform.position.ToGround(), destination) > 0.25f;
                frame++
            )
            {
                Vector2 position = player.transform.position.ToGround();
                Vector2 input = (Vector2)
                    typeof(BotDriver)
                        .GetMethod("NavigateTo", Hidden)
                        .Invoke(bot, new object[] { position, destination });
                Step(input);
                greatestDetour = Mathf.Max(
                    greatestDetour,
                    Mathf.Abs(player.transform.position.z - Origin.z)
                );
            }
            Assert.That(
                greatestDetour,
                Is.GreaterThan(5f),
                "the capsule must pass the wall end, not cut through it"
            );
            Assert.That(
                Vector2.Distance(player.transform.position.ToGround(), destination),
                Is.LessThan(0.3f)
            );
            Assert.That(BotDriver.ActiveRoute, Is.True);
        }

        [Test]
        public void PhysicalPreviewPreservesEnemyStandOffAndFindsAWallTangentEscape()
        {
            Setup(false);
            SetPosition(Vector2.zero);
            Box("west wall", new Vector3(-0.70f, 1.5f, 0), new Vector3(0.5f, 3, 8), "Wall");
            GameObject enemy = Box(
                "east enemy",
                new Vector3(0.924f, 0.9f, 0),
                new Vector3(0.8f, 1.8f, 0.8f),
                "Enemy"
            );
            Physics.SyncTransforms();
            Vector2 before = player.transform.position.ToGround();
            Assert.That(
                movement.PreviewNavigationDelta(Vector2.right * 0.08f).magnitude,
                Is.LessThan(0.01f)
            );
            Assert.That(
                movement.PreviewNavigationDelta(Vector2.left * 0.08f).magnitude,
                Is.LessThan(0.01f)
            );
            Assert.That(
                movement.PreviewNavigationDelta(Vector2.up * 0.08f).y,
                Is.GreaterThan(0.07f)
            );
            Assert.That(
                player.transform.position.ToGround(),
                Is.EqualTo(before),
                "preview cannot move the player"
            );
            for (int frame = 0; frame < 30; frame++)
            {
                Vector2 input = (Vector2)
                    typeof(BotDriver)
                        .GetMethod("NavigateLocal", Hidden)
                        .Invoke(
                            bot,
                            new object[] { player.transform.position.ToGround(), Vector2.left }
                        );
                Step(input);
            }
            Assert.That(
                Mathf.Abs(player.transform.position.z - Origin.z),
                Is.GreaterThan(0.5f),
                "recovery must use the available tangent instead of repeatedly requesting the blocked wall/enemy direction"
            );
            Assert.That(
                Vector2.Distance(
                    player.transform.position.ToGround(),
                    enemy.transform.position.ToGround()
                ),
                Is.GreaterThan(0.85f)
            );
        }

        [Test]
        public void ObjectiveProgressSurvivesReactiveInterruptionsAndRetiresTheRecurringTarget()
        {
            var progress = new BotObjectiveProgress();
            var target = Vector2.right * 3f;
            progress.Pursue(target, Vector2.zero, 0);
            for (int second = 1; second < 20; second++)
            {
                // The policy may switch to retreat/dodge between these observations;
                // selecting the same pickup again cannot reset its progress budget.
                progress.Pursue(target, Vector2.zero, second);
                Assert.That(progress.Observe(Vector2.zero, second), Is.False);
            }
            Assert.That(progress.Observe(Vector2.zero, 20), Is.True);
            Assert.That(progress.IsRetired(target, 79), Is.True);
            Assert.That(progress.IsRetired(target, 80), Is.False);
            progress.Clear();
            progress.Pursue(target, Vector2.zero, 100);
            Assert.That(progress.Observe(Vector2.right, 119), Is.False);
            Assert.That(
                progress.Observe(Vector2.right, 120),
                Is.False,
                "measured progress extends a worthwhile attempt"
            );
        }

        private void Setup(bool divider)
        {
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.5f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.05f;
            var sources = new List<NavMeshBuildSource>
            {
                Source(new Vector3(0, -0.1f, 0), new Vector3(20, 0.2f, 20)),
            };
            if (divider)
            {
                sources.Add(Source(new Vector3(0, 1.5f, 0), new Vector3(1, 3, 10)));
                Box("divider", new Vector3(0, 1.5f, 0), new Vector3(1, 3, 10), "Wall");
            }
            data = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                new Bounds(Origin, new Vector3(24, 10, 24)),
                Vector3.zero,
                Quaternion.identity
            );
            Assert.That(data, Is.Not.Null);
            mesh = NavMesh.AddNavMeshData(data);
            player = new GameObject("geometry test player");
            objects.Add(player);
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.45f;
            player.AddComponent<Rigidbody>().isKinematic = true;
            movement = player.AddComponent<PlayerMovement>();
            Set(movement, "_collider", capsule);
            Set(movement, "_enemyLayerMask", LayerMask.GetMask("Enemy"));
            Set(movement, "_wallLayerMask", LayerMask.GetMask("Wall"));
            var driver = new GameObject("geometry test bot");
            driver.SetActive(false);
            objects.Add(driver);
            bot = driver.AddComponent<BotDriver>();
            Set(bot, "player", player.transform);
            Set(bot, "movement", movement);
            Set(bot, "path", new NavMeshPath());
            Set(bot, "stats", null);
        }

        private static NavMeshBuildSource Source(Vector3 position, Vector3 size) =>
            new()
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(Origin + position, Quaternion.identity, Vector3.one),
                size = size,
                area = 0,
            };

        private GameObject Box(string name, Vector3 position, Vector3 size, string layer)
        {
            var host = new GameObject(name);
            objects.Add(host);
            host.layer = LayerMask.NameToLayer(layer);
            host.transform.position = Origin + position;
            host.AddComponent<BoxCollider>().size = size;
            return host;
        }

        private void SetPosition(Vector2 local)
        {
            player.transform.position = Origin + new Vector3(local.x, 0.9f, local.y);
            Physics.SyncTransforms();
        }

        private void Step(Vector2 input)
        {
            Vector2 delta = movement.PreviewNavigationDelta(input * 4f * Time.fixedDeltaTime);
            player.transform.position += delta.ToWorld();
            Physics.SyncTransforms();
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Hidden).SetValue(target, value);
    }
}
