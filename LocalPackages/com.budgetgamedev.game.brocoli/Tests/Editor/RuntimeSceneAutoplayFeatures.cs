using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        /// <summary>
        /// Drives the agent's loot sweep, doorway tracking, and weapon-range kiting
        /// against the live dungeon, where chests, boosts, and a real player exist.
        /// </summary>
        private static void ExerciseAutoplayObjectives(
            BotDriver bot,
            PlayerStats stats,
            Vector2 position
        )
        {
            Vector2 emptyGround = position + Vector2.one * 500f;
            InvokeHierarchy(bot, "ObserveObjectives", emptyGround);

            var created = new List<GameObject>();
            try
            {
                created.Add(
                    RewardMarker<LootChest>("Coverage bot chest", position + Vector2.right * 2f)
                );
                created.Add(
                    RewardMarker<HealthBoost>("Coverage bot boost", position + Vector2.left * 3f)
                );
                Physics.SyncTransforms();

                SetHierarchyField(
                    bot,
                    "objectives",
                    InvokeHierarchy(bot, "ObserveObjectives", position)
                );
                object enemies = InvokeHierarchy(bot, "ObserveEnemies", position);
                InvokeHierarchy(bot, "Observe", position, enemies);
                InvokeHierarchy(bot, "NavigateIntent", BotIntent.Loot, position, enemies);
                InvokeHierarchy(bot, "NavigateIntent", BotIntent.Collect, position, enemies);
            }
            finally
            {
                foreach (GameObject item in created)
                    Object.DestroyImmediate(item);
            }

            ExerciseRoomTracking(bot, position);
            ExerciseEngageRange(bot, stats, position);
        }

        private static GameObject RewardMarker<T>(string name, Vector2 at)
            where T : Component
        {
            GameObject item = new(name);
            item.transform.position = at.ToWorld();
            item.AddComponent<SphereCollider>().isTrigger = true;
            item.AddComponent<T>();
            return item;
        }

        /// <summary>First room, staying put, then a doorway crossing.</summary>
        private static void ExerciseRoomTracking(BotDriver bot, Vector2 position)
        {
            InvokeHierarchy(bot, "TrackRoom", position);
            InvokeHierarchy(bot, "TrackRoom", position);
            Vector2Int room = DungeonLayout.RoomAt(position);
            InvokeHierarchy(bot, "TrackRoom", DungeonLayout.RoomCenter(room + Vector2Int.right));
            // Back into a room already seen: a doorway crossing, but not progress.
            InvokeHierarchy(bot, "TrackRoom", DungeonLayout.RoomCenter(room));
        }

        /// <summary>Kiting distance comes from the live weapon, or a default without one.</summary>
        private static void ExerciseEngageRange(BotDriver bot, PlayerStats stats, Vector2 position)
        {
            SetHierarchyField(bot, "stats", null);
            InvokeHierarchy(bot, "ObserveEnemies", position);

            SetHierarchyField(bot, "stats", stats);
            float sprayRange = stats.CurrentSprayRange;
            SetHierarchyField(stats, "_currentSprayRange", 0f);
            InvokeHierarchy(bot, "ObserveEnemies", position);
            SetHierarchyField(stats, "_currentSprayRange", sprayRange);
            InvokeHierarchy(bot, "ObserveEnemies", position);
        }

        /// <summary>
        /// The coverage sweep starts a fresh life on death, which needs a real
        /// game-over overlay to press. The press is redirected to a recorder so the
        /// scene the rest of this test is standing in stays where it is.
        /// </summary>
        private static void ExerciseAutoplayRestart(GameOverOverlay overlay)
        {
            GameObject host = new("Coverage restart telemetry");
            try
            {
                RunTelemetry telemetry = host.AddComponent<RunTelemetry>();
                var pressed = new List<GameOverOverlay>();
                telemetry.PressRestart = pressed.Add;

                Assert.That(
                    (bool)InvokeHierarchy(telemetry, "TryRestart"),
                    Is.True,
                    "a visible overlay is the cue to start another life"
                );
                Assert.That(pressed, Is.EqualTo(new[] { overlay }));

                SetHierarchyField(
                    telemetry,
                    "_damage",
                    Object.FindAnyObjectByType<PlayerDamageHandler>()
                );
                InvokeHierarchy(telemetry, "TryRestart");

                // The same guard from inside the run loop, which is where it lives.
                SetHierarchyField(telemetry, "_awaitingRestart", true);
                telemetry.Configure(new AutoplayConfig { Interval = 10000f });
                InvokeHierarchy(telemetry, "Update");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Driving the pause menu leaves an EventSystem behind, which would hide the
        /// game-over overlay's own "there is no EventSystem yet" path for the rest of
        /// the run. Exercise it deliberately rather than depending on the order the
        /// surrounding sweep happens to run in.
        /// </summary>
        private static void ExerciseGameOverEventSystemFallback()
        {
            foreach (
                EventSystem system in Object.FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include
                )
            )
                Object.DestroyImmediate(system.gameObject);

            typeof(GameOverOverlay)
                .GetMethod("EnsureEventSystem", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);

            Assert.That(
                Object.FindAnyObjectByType<EventSystem>(),
                Is.Not.Null,
                "the overlay builds an EventSystem when the scene has none"
            );
        }

        /// <summary>
        /// Once the dungeon is up the session director's work is done, which is a
        /// branch only reachable from inside the dungeon scene.
        /// </summary>
        private static void ExerciseAutoplaySessionDirector()
        {
            GameObject host = new("Coverage session director");
            try
            {
                AutoplaySessionDirector director = host.AddComponent<AutoplaySessionDirector>();
                InvokeHierarchy(director, "Start");
                InvokeHierarchy(director, "Update");

                Assert.That(director.enabled, Is.False, "the dungeon is already loaded");
                InvokeHierarchy(director, "OnApplicationQuit");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Runs every feature probe against the real overlays and pause menu, then
        /// runs each one again in the state where it has nothing to do -- which is
        /// how the probes behave for most of a run.
        /// </summary>
        private static void ExerciseAutoplayFeatureSweep()
        {
            GameObject host = new("Coverage feature director");
            try
            {
                AutoplayFeatureDirector director = host.AddComponent<AutoplayFeatureDirector>();
                InvokeHierarchy(director, "Start");

                InvokeHierarchy(director, "OpenInventory");
                InvokeHierarchy(director, "OpenInventory");
                InvokeHierarchy(director, "NavigateInventory");
                InvokeHierarchy(director, "EquipInventoryItem");
                InvokeHierarchy(director, "OpenMap");
                InvokeHierarchy(director, "PanMap");
                InvokeHierarchy(director, "CloseOverlay");
                InvokeHierarchy(director, "CloseOverlay");
                InvokeHierarchy(director, "NavigateInventory");
                InvokeHierarchy(director, "EquipInventoryItem");
                InvokeHierarchy(director, "PanMap");

                InvokeHierarchy(director, "OpenPauseMenu");
                InvokeHierarchy(director, "OpenPauseMenu");
                InvokeHierarchy(director, "OpenInventory");
                InvokeHierarchy(director, "OpenMap");
                InvokeHierarchy(director, "OpenPauseSettings");
                InvokeHierarchy(director, "ResumeFromPause");
                InvokeHierarchy(director, "ResumeFromPause");
                InvokeHierarchy(director, "OpenPauseSettings");
                InvokeHierarchy(director, "ProbeSaveRoundTrip");

                for (int step = 0; step <= 11; step++)
                {
                    SetHierarchyField(director, "nextProbeTime", 0f);
                    InvokeHierarchy(director, "Update");
                }

                Assert.That(director.CompletedSweeps, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(host);
                ExplorationOverlay.EnsurePresent()?.Close();
                Time.timeScale = 1f;
            }
        }
    }
}
