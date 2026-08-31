using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseEnemyPlacementPolicies(List<EnemyBase> enemies)
        {
            foreach (
                string name in new[]
                {
                    "Hydra",
                    "HardChunky",
                    "ShootingHard",
                    "Hard",
                    "Shooting",
                    "Normal",
                    "Spider",
                    "Easy",
                }
            )
                Assert.That(DungeonEnemyPlacer.MinRingFor(name), Is.GreaterThan(0));

            var open = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.GrandArena,
                DungeonLayout.RoomTheme.Empty,
                10f,
                8f,
                0
            );
            var dividedVertical = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.Divided,
                DungeonLayout.RoomTheme.Empty,
                10f,
                8f,
                0
            );
            var dividedHorizontal = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.Divided,
                DungeonLayout.RoomTheme.Empty,
                10f,
                8f,
                1
            );
            Assert.That(DungeonEnemyPlacer.IsOnDivider(Vector2.zero, open), Is.False);
            DungeonEnemyPlacer.IsOnDivider(Vector2.zero, dividedVertical);
            DungeonEnemyPlacer.IsOnDivider(Vector2.zero, dividedHorizontal);
            for (int seed = 0; seed < 100; seed++)
            {
                DungeonEnemyPlacer.PickSpot(Vector2.zero, dividedVertical, new System.Random(seed));
                DungeonEnemyPlacer.PickSpot(
                    Vector2.zero,
                    dividedHorizontal,
                    new System.Random(seed)
                );
            }

            var allowed = enemies.FindAll(enemy => enemy != null);
            if (allowed.Count == 0)
                return;
            string originalName = allowed[0].name;
            allowed[0].name = "Coverage Spider";
            EnemyBase spider = DungeonEnemyPlacer.FindSpider(allowed);
            Assert.That(spider, Is.SameAs(allowed[0]));
            Assert.That(
                DungeonEnemyPlacer.PickEnemy(allowed, spider, open, 1, new System.Random(1)),
                Is.SameAs(spider)
            );
            DungeonEnemyPlacer.PickEnemy(allowed, null, open, 0, new System.Random(1));
            DungeonEnemyPlacer.PickEnemy(allowed, null, open, 1, new System.Random(1));
            allowed[0].name = "Coverage Enemy";
            DungeonEnemyPlacer.PickEnemy(
                new List<EnemyBase> { allowed[0] },
                null,
                open,
                0,
                new System.Random(1)
            );
            allowed[0].name = originalName;
            DungeonEnemyPlacer.FindSpider(allowed);
        }

        private static void ExerciseAutosave()
        {
            SetStaticField(typeof(AutoplayController), "<IsActive>k__BackingField", false);
            BrocoliAutosaveController.EnsurePresent();
            BrocoliAutosaveController autosave =
                Object.FindAnyObjectByType<BrocoliAutosaveController>();
            if (autosave == null)
                return;

            InvokeHierarchy(autosave, "EnsurePresentInDungeon");
            GameObject duplicateObject = new("Coverage Duplicate Autosave");
            BrocoliAutosaveController duplicate =
                duplicateObject.AddComponent<BrocoliAutosaveController>();
            InvokeHierarchy(duplicate, "Awake");
            InvokeHierarchy(duplicate, "OnDestroy");

            object[] capture = { null };
            if ((bool)InvokeHierarchy(autosave, "TryCapture", capture))
            {
                SetStaticField(typeof(BrocoliSaveSystem), "pendingContinue", capture[0]);
                PlayerController controller = Object.FindAnyObjectByType<PlayerController>();
                GameStates game = Object.FindAnyObjectByType<GameStates>();
                InvokeHierarchy(controller, "Start");
                InvokeHierarchy(game, "Start");
                SetStaticField(typeof(BrocoliSaveSystem), "pendingContinue", capture[0]);
                Drain((System.Collections.IEnumerator)InvokeHierarchy(autosave, "Start"));
            }

            SetHierarchyField(autosave, "ready", false);
            InvokeHierarchy(autosave, "SaveCheckpoint");
            SetHierarchyField(autosave, "ready", true);
            SetHierarchyField(autosave, "nextSaveTime", 0f);
            InvokeHierarchy(autosave, "Update");
            InvokeHierarchy(autosave, "OnApplicationPause", false);
            InvokeHierarchy(autosave, "OnApplicationPause", true);
            InvokeHierarchy(autosave, "OnApplicationFocus", true);
            InvokeHierarchy(autosave, "OnApplicationFocus", false);
            InvokeHierarchy(autosave, "OnApplicationQuit");
            BrocoliAutosaveController.SaveNow();
            InvokeHierarchy(autosave, "TryCapture", new object[] { null });
        }

        private static void ExerciseDungeonCoreServices(List<EnemyBase> enemies)
        {
            GameStates game = Object.FindAnyObjectByType<GameStates>();
            SetHierarchyField(game, "lastSecond", 0);
            SetHierarchyField(game, "lastTenSecondMilestone", 0);
            game.gameTime = 10f;
            InvokeHierarchy(game, "Update");

            EnemySpatialHash hash = EnemySpatialHash.Instance;
            EnemyBase enemy = enemies.Find(candidate => candidate != null);
            if (hash != null && enemy != null)
            {
                hash.Register(enemy);
                Vector3 original = enemy.transform.position;
                enemy.transform.position += Vector3.right * 100f;
                hash.UpdatePosition(enemy);
                hash.GetNearbyEnemies(enemy.transform.position.ToGround(), 1f);
                enemy.transform.position = original;
                hash.UpdatePosition(enemy);
            }

            GameContext context = GameContext.Instance;
            InvokeHierarchy(context, "OnApplicationQuit");
            Assert.That(GameContext.Instance, Is.Null);
            GameContext.ResetInstance();
            _ = GameContext.Instance;
        }
    }
}
