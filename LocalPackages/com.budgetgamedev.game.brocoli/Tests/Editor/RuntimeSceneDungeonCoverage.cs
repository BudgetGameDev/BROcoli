using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseDungeonSystems(PlayerStats stats, List<EnemyBase> enemies)
        {
            DungeonManager manager = Object.FindAnyObjectByType<DungeonManager>();
            Assert.That(manager, Is.Not.Null);
            manager.CopyVisitedRooms(null);
            var visited = new List<Vector2Int>();
            manager.CopyVisitedRooms(visited);
            Assert.That(visited, Is.Not.Empty);
            SetHierarchyField(manager, "player", stats.transform);
            SetHierarchyField(manager, "hasCurrentRoom", false);
            SetHierarchyField(manager, "nextRoomCheck", 0f);
            Vector3 savedPlayerPosition = stats.transform.position;
            stats.transform.position = new Vector3(
                savedPlayerPosition.x,
                savedPlayerPosition.y,
                1000f
            );
            InvokeHierarchy(manager, "Update");
            stats.transform.position = savedPlayerPosition;
            SetHierarchyField(manager, "nextRoomCheck", 0f);
            InvokeHierarchy(manager, "Update");
            ExerciseEnemyPlacementPolicies(enemies);
            ExercisePropClusters();
            ExerciseDungeonCoreServices(enemies);
            InvokeHierarchy(manager, "Update");
            SetHierarchyField(manager, "nextRoomCheck", 0f);
            SetHierarchyField(manager, "player", null);
            InvokeHierarchy(manager, "Update");
            InvokeHierarchy(manager, "ResolvePlayer");
            InvokeHierarchy(manager, "LoadEnemyPrefabs");
            LogAssert.Expect(
                LogType.Error,
                "DungeonManager: no enemy prefabs in 'Brocoli/CursedDevolpmentStudioAss Assets/Waves'."
            );
            manager.LoadEnemyPrefabs(new GameObject[] { null });
            InvokeHierarchy(manager, "LoadEnemyPrefabs");
            SetStaticField(
                typeof(BrocoliSaveSystem),
                "pendingContinue",
                new BrocoliRunSave { playerPosition = stats.transform.position, dungeon = null }
            );
            manager.ResolveInitialRoom();
            BrocoliSaveSystem.FinishContinue();
            InvokeHierarchy(manager, "EnterRoom", manager.CurrentRoom);
            InvokeHierarchy(manager, "GetState", manager.CurrentRoom);
            InvokeHierarchy(manager, "GetState", new Vector2Int(50, 50));
            InvokeHierarchy(manager, "MarkVisited", manager.CurrentRoom);
            InvokeHierarchy(manager, "EnsureRoom", manager.CurrentRoom);
            InvokeHierarchy(manager, "CollectDistantRooms", manager.CurrentRoom);
            InvokeHierarchy(manager, "IsDistantFromCurrentRoom", manager.CurrentRoom);
            InvokeHierarchy(manager, "IsDistantFromCurrentRoom", new Vector2Int(100, 100));
            InvokeHierarchy(manager, "UnloadRoom", new Vector2Int(100, 100));
            InvokeHierarchy(manager, "PruneSharedGeometry");
            object save = InvokeHierarchy(manager, "CaptureRunState");
            InvokeHierarchy(manager, "RestoreRunState", new object[] { null });
            InvokeHierarchy(manager, "RestoreRunState", save);
            InvokeHierarchy(
                manager,
                "RestoreRunState",
                new BrocoliDungeonSave { seed = manager.Seed, rooms = null }
            );
            InvokeHierarchy(
                manager,
                "RestoreRunState",
                new BrocoliDungeonSave
                {
                    seed = manager.Seed,
                    rooms = new List<BrocoliRoomSave>
                    {
                        null,
                        new()
                        {
                            x = 1,
                            y = 2,
                            visited = true,
                            openedChestSlots = new List<int> { -1, 0, 2 },
                        },
                    },
                }
            );
            InvokeHierarchy(manager, "RestoreRunState", save);
            SetHierarchyField(manager, "isRoomStreaming", true);
            InvokeHierarchy(manager, "RequestRoomStreaming");
            SetHierarchyField(manager, "isRoomStreaming", false);
            SetHierarchyField(manager, "unloadDistance", 0);
            Drain((System.Collections.IEnumerator)InvokeHierarchy(manager, "StreamRooms"));
            var interrupted = (System.Collections.IEnumerator)InvokeHierarchy(
                manager,
                "StreamRooms"
            );
            Assert.That(interrupted.MoveNext(), Is.True);
            SetHierarchyField(
                manager,
                "streamingRevision",
                GetHierarchyField<int>(manager, "streamingRevision") + 1
            );
            interrupted.MoveNext();
            SetHierarchyField(manager, "unloadDistance", 2);
            ExerciseStreamingRevisionDuringNavmesh(manager);
            ExerciseCameraOcclusionFader();

            EnemyBase enemy = enemies.Find(candidate => candidate != null);
            if (enemy != null)
                ExerciseNavigator(enemy, stats.transform);
            ExerciseAutoplay(stats);
            ExerciseAutosave();
            ExerciseLootChest(stats);
        }

        private static void ExercisePropClusters()
        {
            DungeonPropPlacer placer = Object.FindAnyObjectByType<DungeonPropPlacer>();
            Assert.That(placer, Is.Not.Null);
            System.Type occupiedType = typeof(DungeonPropPlacer).GetNestedType(
                "OccupiedSpot",
                System.Reflection.BindingFlags.NonPublic
            );
            object occupied = System.Activator.CreateInstance(
                typeof(List<>).MakeGenericType(occupiedType)
            );
            GameObject parent = new("Coverage Prop Clusters");
            var room = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.OpenHall,
                DungeonLayout.RoomTheme.Sparse,
                12f,
                8f,
                0
            );
            InvokeHierarchy(
                placer,
                "PlaceSmallClusters",
                parent.transform,
                Vector2.zero,
                room,
                new System.Random(1),
                occupied,
                1,
                3,
                3,
                null
            );
            InvokeHierarchy(
                placer,
                "PlaceSmallClusters",
                parent.transform,
                Vector2.zero,
                room,
                new System.Random(2),
                occupied,
                1,
                3,
                3,
                new[] { DungeonPropTokens.Coin }
            );
            Object.Destroy(parent);
        }

        private static void ExerciseNavigator(EnemyBase enemy, Transform player)
        {
            DungeonEnemyNavigator navigator = enemy.GetComponent<DungeonEnemyNavigator>();
            if (navigator == null)
                navigator = enemy.gameObject.AddComponent<DungeonEnemyNavigator>();
            InvokeHierarchy(navigator, "InitializeRecovery");
            InvokeHierarchy(navigator, "ResetRecovery");
            enemy.enabled = false;
            InvokeHierarchy(navigator, "FixedUpdate");
            enemy.enabled = true;
            enemy.player = null;
            SetHierarchyField(navigator, "realPlayer", null);
            InvokeHierarchy(navigator, "FixedUpdate");
            enemy.player = player;
            SetHierarchyField(navigator, "realPlayer", player);
            SetHierarchyField(navigator, "recoveryUntil", Time.time + 10f);
            SetHierarchyField(navigator, "recoveryTarget", player.position + Vector3.left);
            InvokeHierarchy(navigator, "FixedUpdate");
            SetHierarchyField(navigator, "recoveryUntil", 0f);
            SetHierarchyField(navigator, "nextRepath", Time.time + 10f);
            InvokeHierarchy(navigator, "FixedUpdate");
            SetHierarchyField(navigator, "nextRepath", 0f);
            SetHierarchyField(navigator, "nextProgressCheck", 0f);
            InvokeHierarchy(navigator, "FixedUpdate");
            navigator.RepathNow();
            InvokeHierarchy(navigator, "SteerTowardPlayer");
            Vector3 enemyPosition = enemy.transform.position;
            enemy.transform.position = player.position + Vector3.right * 0.5f;
            InvokeHierarchy(navigator, "SteerTowardPlayer");
            enemy.transform.position = player.position + Vector3.up * 1000f;
            InvokeHierarchy(navigator, "SteerTowardPlayer");
            enemy.transform.position = enemyPosition;
            InvokeHierarchy(
                navigator,
                "SteerDirectlyOrSlide",
                enemy.transform.position,
                player.position
            );
            InvokeHierarchy(navigator, "SetProxyTarget", player.position + Vector3.right);
            InvokeHierarchy(navigator, "CheckProgress");
            SetHierarchyField(navigator, "stationaryTime", 10f);
            InvokeHierarchy(navigator, "CheckProgress");
            object[] recovery = { Vector3.zero };
            InvokeHierarchy(navigator, "TryPickRecoveryTarget", recovery);
            object[] slide =
            {
                enemy.transform.position,
                enemy.transform.position,
                enemy.transform.position,
                Vector3.zero,
            };
            InvokeHierarchy(navigator, "TryGetObstacleSlide", slide);
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Coverage Navigation Obstacle";
            Vector3 from = enemy.transform.position;
            obstacle.transform.position = from + Vector3.right * 0.9f + Vector3.up * 0.75f;
            Physics.SyncTransforms();
            object[] obstacleSlide = { from, from + Vector3.right * 3f, from, Vector3.zero };
            InvokeHierarchy(navigator, "TryGetObstacleSlide", obstacleSlide);
            InvokeHierarchy(navigator, "IsNavigationObstacle", (object)null);
            InvokeHierarchy(navigator, "IsNavigationObstacle", enemy.GetComponent<Collider>());
            InvokeHierarchy(navigator, "IsNavigationObstacle", player.GetComponent<Collider>());
            Object.Destroy(obstacle);
            InvokeHierarchy(navigator, "OnDisable");
            InvokeHierarchy(navigator, "OnDestroy");
        }

        private static void ExerciseLootChest(PlayerStats stats)
        {
            Assert.That(LootChest.ScaledExperiencePerPickup(0, 0, 0), Is.GreaterThan(0));
            LootChest.PickWeightedBoost(null);
            LootChest.PickWeightedBoost(new GameObject[0]);
            GameObject invalid = new("Invalid Boost");
            LootChest.PickWeightedBoost(new[] { invalid, null });
            Object.Destroy(invalid);
            GameObject[] assets = Resources.LoadAll<GameObject>(
                "Brocoli/CursedDevolpmentStudioAss Assets"
            );
            LootChest.PickWeightedBoost(assets);
            ExerciseChestExperienceDistribution();

            BoostHandler boostHandler = Object.FindAnyObjectByType<BoostHandler>();
            GameObject[] originalBoosts = null;
            if (boostHandler != null)
            {
                originalBoosts = GetHierarchyField<GameObject[]>(boostHandler, "_boosters");
                SetHierarchyField(boostHandler, "_boosters", null);
            }

            GameObject chestObject = new("Coverage Loot Chest");
            LootChest chest = chestObject.AddComponent<LootChest>();
            chestObject.AddComponent<BoxCollider>();
            SetHierarchyField(chest, "expDropCount", 2);
            SetHierarchyField(chest, "boostDropCount", 1);
            chest.ConfigureForRoom(20);
            SetHierarchyField(chest, "expDropCount", 0);
            InvokeHierarchy(chest, "SpawnLoot", stats);
            if (boostHandler != null)
                SetHierarchyField(boostHandler, "_boosters", new[] { new GameObject("Invalid") });
            InvokeHierarchy(chest, "SpawnLoot", stats);
            if (boostHandler != null)
                SetHierarchyField(boostHandler, "_boosters", originalBoosts);
            SetHierarchyField(chest, "expDropCount", 2);
            Collider playerCollider = stats.GetComponent<Collider>();
            InvokeHierarchy(chest, "OnTriggerEnter", playerCollider);
            InvokeHierarchy(chest, "OnTriggerEnter", playerCollider);

            GameObject animationObject = new("Coverage Loot Chest Animation");
            LootChest animation = animationObject.AddComponent<LootChest>();
            Drain((System.Collections.IEnumerator)InvokeHierarchy(animation, "PopAndVanish"));
        }
    }
}
