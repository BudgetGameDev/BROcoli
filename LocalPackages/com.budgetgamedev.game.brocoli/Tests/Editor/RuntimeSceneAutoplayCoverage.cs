using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static AutoplayController LaunchAutoplay() =>
            AutoplayController.Bootstrap(
                new AutoplayConfig
                {
                    Enabled = true,
                    Duration = 100000f,
                    Interval = 100000f,
                    OutDir = Path.Combine(Path.GetTempPath(), "brocoli-autoplay-scene-coverage"),
                    Deterministic = true,
                    Timestep = 1f / 60f,
                    Scenario = "smoke",
                }
            );

        private static void DisableAutoplay(AutoplayController launch)
        {
            foreach (Behaviour behaviour in launch.GetComponents<Behaviour>())
                if (behaviour != launch)
                    behaviour.enabled = false;
        }

        private static void ExerciseAutoplay(PlayerStats stats)
        {
            BotDriver bot = Object.FindAnyObjectByType<BotDriver>();
            Assert.That(bot, Is.Not.Null);
            bot.enabled = true;
            InvokeHierarchy(bot, "ResolveWorld");
            Vector2 position = stats.transform.position.ToGround();
            SetStaticField(typeof(EnemySpatialHash), "_applicationIsQuitting", true);
            InvokeHierarchy(bot, "ObserveEnemies", position);
            SetStaticField(typeof(EnemySpatialHash), "_applicationIsQuitting", false);
            object observation = InvokeHierarchy(bot, "ObserveEnemies", position);
            InvokeHierarchy(bot, "NavigateCombat", position, observation, false);
            InvokeHierarchy(bot, "NavigateCombat", position, observation, true);
            ExerciseCombatNavigationStates(bot, position);
            ExerciseProjectileDodges(bot, position);
            InvokeHierarchy(bot, "ComputeProjectileDodge", position);
            InvokeHierarchy(bot, "GetExplorationTarget", position);
            InvokeHierarchy(bot, "PickExplorationRoom", DungeonLayout.RoomAt(position));
            InvokeHierarchy(bot, "NavigateTo", position, position + Vector2.right);
            InvokeHierarchy(bot, "NavigateLocal", position, Vector2.up);
            object[] clearance = { position, Vector2.zero, 0f };
            InvokeHierarchy(bot, "TryMeasureClearance", clearance);
            InvokeHierarchy(bot, "IsNavigationObstacle", (object)null);
            InvokeHierarchy(bot, "TrackProgress", position);
            SetHierarchyField(bot, "nextProgressCheck", 0f);
            SetHierarchyField(bot, "<Move>k__BackingField", Vector2.one);
            SetHierarchyField(bot, "stationaryTime", 10f);
            InvokeHierarchy(bot, "TrackProgress", position);
            InvokeHierarchy(bot, "BeginStuckRecovery");
            // Enough unsticking in one room to write the destination off, once with a
            // room picked and once without.
            for (int attempt = 0; attempt < 10; attempt++)
                InvokeHierarchy(bot, "BeginStuckRecovery");
            InvokeHierarchy(bot, "PickExplorationRoom", DungeonLayout.RoomAt(position));
            for (int attempt = 0; attempt < 10; attempt++)
                InvokeHierarchy(bot, "BeginStuckRecovery");

            // Reaching a room already known is not progress, so the give-up clocks
            // keep running; and a target written off with nothing left to write off
            // sends the agent to the middle of the room to unwedge itself.
            InvokeHierarchy(bot, "TrackRoom", position);
            SetHierarchyField(bot, "lastProgress", -10000f);
            SetHierarchyField(bot, "lastCombatProgress", -10000f);
            InvokeHierarchy(bot, "GetExplorationTarget", position);
            SetHierarchyField(bot, "unwedgeUntil", float.MaxValue);
            InvokeHierarchy(bot, "GetExplorationTarget", position);
            SetHierarchyField(bot, "unwedgeUntil", 0f);
            InvokeHierarchy(bot, "FixedUpdate");
            // A tick with a projectile already inbound, which is the one case that
            // records a dodge. The frame counter is parked so the cached dodge
            // survives into the decision instead of being recomputed away.
            SetHierarchyField(bot, "lastDodge", Vector2.right);
            SetHierarchyField(bot, "frame", 0);
            // Stuck recovery outranks everything, and the sweep above started one.
            SetHierarchyField(bot, "recoveryUntil", 0f);
            // Combat progress: once with something new to record, once with nothing.
            InvokeHierarchy(bot, "TrackCombatProgress");
            InvokeHierarchy(bot, "TrackCombatProgress");
            InvokeHierarchy(bot, "FixedUpdate");
            ExerciseAutoplayObjectives(bot, stats, position);
            bot.enabled = false;
            ExerciseAutoplaySessionDirector();
            ExerciseAutoplayFeatureSweep();
            ExerciseGameOverEventSystemFallback();

            GameObject frameObject = new("Coverage Frame Capture");
            FrameCapture unconfigured = frameObject.AddComponent<FrameCapture>();
            InvokeHierarchy(unconfigured, "Start");
            var config = new AutoplayConfig
            {
                OutDir = Path.Combine(Path.GetTempPath(), "brocoli-autoplay-coverage"),
                Interval = 0f,
                Seed = 42,
            };
            FrameCapture configured = frameObject.AddComponent<FrameCapture>();
            configured.Configure(config);

            GameObject controllerObject = new("Coverage Autoplay Controller");
            AutoplayController controller = controllerObject.AddComponent<AutoplayController>();
            SetHierarchyField(controller, "_config", config);
            InvokeHierarchy(controller, "OnSceneLoaded", default(Scene), LoadSceneMode.Single);
            InvokeHierarchy(
                controller,
                "OnSceneLoaded",
                SceneManager.GetActiveScene(),
                LoadSceneMode.Single
            );
            // OnSceneLoaded wires the gameplay-scoped autoplay components -- frame
            // capture is not among them, because it belongs to the whole session and
            // is added when the run begins. Keep this integration test from letting
            // telemetry observe the deliberate game-over exercised later in the smoke
            // test, because telemetry correctly exits Play Mode at the end of a run.
            controllerObject.GetComponent<RunTelemetry>().enabled = false;
            controllerObject.GetComponent<BotDriver>().enabled = false;
            controllerObject.GetComponent<LevelUpAutoResolver>().enabled = false;
            InvokeHierarchy(
                controller,
                "OnSceneLoaded",
                SceneManager.GetActiveScene(),
                LoadSceneMode.Single
            );
        }

        private static void ExerciseCombatNavigationStates(BotDriver bot, Vector2 position)
        {
            object Create(float distance, Vector2 nearest, Vector2 centroid, Vector2 repulsion) =>
                new BotDriver.EnemyObservation(
                    1,
                    1,
                    distance,
                    nearest,
                    nearest,
                    centroid,
                    repulsion
                );

            // The kiting band now follows the live weapon, so read what the agent
            // would actually use rather than the serialized fallback.
            float engage = (float)InvokeHierarchy(bot, "get_EngageRange");
            InvokeHierarchy(
                bot,
                "NavigateCombat",
                position,
                Create(engage + 2f, position + Vector2.right * 3f, position, Vector2.zero),
                false
            );
            InvokeHierarchy(
                bot,
                "NavigateCombat",
                position,
                Create(0f, position, position, Vector2.one),
                false
            );
            InvokeHierarchy(
                bot,
                "NavigateCombat",
                position,
                Create(engage, position + Vector2.right, position + Vector2.left, Vector2.zero),
                false
            );
        }

        private static void ExerciseProjectileDodges(BotDriver bot, Vector2 position)
        {
            var objects = new System.Collections.Generic.List<GameObject>();
            GameObject Projectile(string name, Vector2 offset, Vector2 velocity)
            {
                GameObject item = new(name);
                item.transform.position = (position + offset).ToWorld(0f);
                Rigidbody body = item.AddComponent<Rigidbody>();
                item.AddComponent<SphereCollider>().isTrigger = true;
                item.AddComponent<EnemyProjectile>();
                body.useGravity = false;
                body.SetGroundVelocity(velocity);
                objects.Add(item);
                return item;
            }

            float sense = GetHierarchyField<float>(bot, "projectileSenseRadius");
            float dodge = GetHierarchyField<float>(bot, "dodgeRadius");
            GameObject ordinary = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ordinary.transform.position = position.ToWorld(0f);
            objects.Add(ordinary);
            Projectile("Coverage stationary projectile", Vector2.left * 2f, Vector2.zero);
            Projectile("Coverage departing projectile", Vector2.left * 2f, Vector2.left);
            Projectile("Coverage direct projectile", Vector2.left * 2f, Vector2.right);
            Projectile(
                "Coverage offset projectile",
                Vector2.left * 2f + Vector2.up * (dodge * 0.25f),
                Vector2.right
            );
            Projectile(
                "Coverage missing projectile",
                Vector2.left * 2f + Vector2.up * Mathf.Min(dodge + 0.1f, sense - 2.1f),
                Vector2.right
            );
            Physics.SyncTransforms();
            InvokeHierarchy(bot, "ComputeProjectileDodge", position);
            foreach (GameObject item in objects)
                Object.Destroy(item);
        }

        private static void SetStaticField(System.Type type, string name, object value)
        {
            type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        }
    }
}
