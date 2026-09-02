using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private const string MenuScene =
            "Packages/com.budgetgamedev.game.brocoli/Scenes/Brocoli_MainMenu.unity";

        [UnityTest]
        [TestMustExpectAllLogs(false)]
        public IEnumerator ShippingScenesBootAndAdvanceWithoutRuntimeErrors()
        {
            PlayerPrefs.SetInt("ShowVirtualController", 0);
            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Brocoli_MainMenu_Common"));
            ExerciseSharedRuntime();
            ExerciseMainMenu();

            AutoplayController launch = LaunchAutoplay();
            yield return null;
            DisableAutoplay(launch);
            GameObject automation = new("Coverage Autoplay Drivers");
            automation.AddComponent<BotDriver>();
            automation.AddComponent<LevelUpAutoResolver>();
            for (int frame = 0; frame < 90; frame++)
                yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Brocoli_Dungeon_Common"));
            Assert.That(Object.FindAnyObjectByType<GameStates>(), Is.Not.Null);
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            Assert.That(stats, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<DungeonManager>(), Is.Not.Null);
            automation.GetComponent<BotDriver>().enabled = false;
            List<EnemyBase> enemies = ExerciseEnemyCatalog(stats.transform.position);
            for (int frame = 0; frame < 120; frame++)
                yield return null;
            ExerciseDungeon(stats, enemies);
            foreach (EnemyBase enemy in enemies)
                if (enemy != null)
                    enemy.Die();
            for (int frame = 0; frame < 45; frame++)
                yield return null;
            ReturnEnemyCatalog(enemies);
            ExercisePreloaderAndPools();
            ExerciseExpGain(stats);
            for (int hit = 0; hit < 10 && stats.IsAlive; hit++)
                stats.ApplyDamage(10000f);
            Assert.That(stats.IsAlive, Is.False);
            PlayerDamageHandler damage = Object.FindAnyObjectByType<PlayerDamageHandler>();
            Assert.That(damage, Is.Not.Null);
            damage.CheckForDeath();
            for (int frame = 0; frame < 90; frame++)
                yield return null;
            Assert.That(damage.IsGameOver, Is.True);
            GameOverOverlay gameOver = GameOverOverlay.Show(-1, -2, -3, -4f);
            ExerciseAutoplayRestart(gameOver);
            Assert.That(gameOver.DisplayedScore, Is.Zero);
            _ = gameOver.RestartButton;
            _ = gameOver.MainMenuButton;
            Invoke(gameOver, "SelectButton", 0);
            Invoke(gameOver, "SelectButton", 1);
            Invoke(gameOver, "Update");

            gameOver.RestartGame();
            for (int frame = 0; frame < 90; frame++)
                yield return null;
            yield return ReturnThroughPauseAndGameOverMenus();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Brocoli_MainMenu_Common"));
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PlayerPrefs.DeleteKey("ShowVirtualController");
            GameContext.ResetInstance();
        }

        private static void ExerciseMainMenu()
        {
            ResponsiveMainMenuLayout layout =
                Object.FindAnyObjectByType<ResponsiveMainMenuLayout>();
            if (layout == null)
                layout = Object
                    .FindAnyObjectByType<MainMenu>()
                    ?.gameObject.AddComponent<ResponsiveMainMenuLayout>();
            Assert.That(layout, Is.Not.Null);
            ExerciseMainMenuInput(layout);
            ExerciseSettingsInput(layout);
            ExerciseHdrDetails(layout);
            ExerciseHdrCalibration(layout);
            Invoke(layout, "OnRectTransformDimensionsChange");
            Invoke(layout, "OpenSettings");
            Invoke(layout, "SyncVolumeControls");
            Invoke(layout, "SelectSetting", 0, false);
            Invoke(layout, "SelectSetting", 2, false);
            Invoke(layout, "CloseSettings");
            Invoke(layout, "OpenCredits");
            Invoke(layout, "UpdateCreditsInput");
            Invoke(layout, "CloseCredits");
            Invoke(layout, "OpenSaves");
            ExerciseSavesEdges(layout);
            Invoke(layout, "FocusAction", 0);
            Invoke(layout, "FocusAction", 1);
            Invoke(layout, "CloseSaves");
            Invoke(layout, "ApplyResponsiveLayout", true);
        }

        private static void ExerciseDungeon(PlayerStats stats, List<EnemyBase> enemies)
        {
            ExerciseVirtualController();
            ExerciseShuffleWalkVisual(stats);
            ExercisePlayerMovement(stats);
            ExercisePlayerCombat(stats, enemies);
            ExerciseDungeonSystems(stats, enemies);
            ExplorationOverlay exploration = ExplorationOverlay.EnsurePresent();
            Assert.That(exploration, Is.Not.Null);
            ExerciseExplorationOverlay(exploration);
            Invoke(exploration, "Open", ExplorationOverlay.Pane.Inventory);
            ExerciseInventoryNavigationEdges(exploration);
            Invoke(exploration, "EnsureInventorySelection");
            Invoke(exploration, "MoveInventorySelection", Vector2.right);
            Invoke(exploration, "MoveInventorySelection", Vector2.down);
            Invoke(exploration, "TransferSelectedInventoryItem");
            Invoke(exploration, "EquipSelectedInventoryItem");
            Invoke(exploration, "SwitchPane", 1);
            DungeonMapGraphic map = Object.FindAnyObjectByType<DungeonMapGraphic>();
            if (map != null)
            {
                ExerciseDungeonMap(map);
                map.RefreshFromDungeon(true);
                map.FocusPlayer();
                map.Pan(new Vector2(0.5f, -0.25f));
                map.ZoomBy(0.2f);
            }
            Invoke(exploration, "SwitchPane", -1);
            exploration.Close();
            PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>();
            Assert.That(pause, Is.Not.Null);
            ExercisePauseMenu(pause);
            pause.Pause();
            Assert.That(pause.IsPaused(), Is.True);
            pause.Resume();
            Assert.That(pause.IsPaused(), Is.False);
            Invoke(pause, "SelectMenuButton", 0);
            Invoke(pause, "SelectMenuButton", 1);
            Invoke(pause, "UpdateSelectionVisuals");

            stats.ResetStats();
            foreach (TemporaryBoostType type in System.Enum.GetValues(typeof(TemporaryBoostType)))
            {
                stats.ApplyTemporaryBoost(type, 0.1f, 0.25f);
                Assert.That(stats.HasActiveBoost(type), Is.True);
            }
            stats.AddSprayRange(0.5f);
            stats.AddSprayWidth(0.5f);
            stats.AddSprayDamageMultiplier(0.1f);
            stats.AddMaxHealth(5f);
            stats.AddDamagePublic(1f);
            stats.AddSpeedPublic(0.1f);
            stats.AddAttackSpeedPublic(0.05f);
            stats.AddDetectionRadiusPublic(0.5f);
            stats.AddCritChance(1f);
            stats.AddCritDamage(0.1f);
            stats.AddDodgeChance(1f);
            stats.AddArmor(1f);
            stats.AddHealthRegen(1f);
            stats.AddLifeSteal(1f);
            stats.ApplyDamage(1f);
            stats.ApplyLifeSteal(10f);
            stats.CalculateDamageOutput(10f, out _);
            stats.ComputePowerScore();
            ExercisePlayerStats(stats);
            ExerciseBoostCatalog(stats);

            LevelUpScreen levelUp = Object.FindAnyObjectByType<LevelUpScreen>();
            Assert.That(levelUp, Is.Not.Null);
            ExerciseLevelUpScreen(levelUp, stats);
            levelUp.Show(2, stats);
            Assert.That(levelUp.GetOption(0), Is.Not.Null);
            levelUp.AutoSelectUpgrade(0);
            levelUp.ProcessKeyboardShortcuts(true, false, false);
            levelUp.ProcessKeyboardShortcuts(false, true, false);
            levelUp.ProcessKeyboardShortcuts(false, false, true);
            levelUp.Hide();

            SanitizerSpray spray = Object.FindAnyObjectByType<SanitizerSpray>();
            Assert.That(spray, Is.Not.Null);
            ExerciseSpray(spray, enemies);
            spray.UpdateStatsFromPlayer();
            spray.StartSpray(Vector2.right);
            spray.StopSpray();
            spray.FireSprayBurst(Vector2.up, 0.05f);

            DiabloHud hud = DiabloHud.EnsurePresent();
            if (hud != null)
            {
                GameObject duplicateHud = new("Coverage Duplicate Diablo HUD");
                duplicateHud.AddComponent<DiabloHud>();
                Object.Destroy(duplicateHud);
                if (enemies.Count > 1 && enemies[0] != null && enemies[1] != null)
                {
                    enemies[0].isElite = false;
                    enemies[1].isElite = false;
                    Invoke(hud, "ShowEnemy", enemies[0]);
                    Invoke(hud, "ShowEnemy", enemies[1]);
                    enemies[1].isElite = true;
                    Invoke(hud, "ShowEnemy", enemies[1]);
                }
            }

            EnemyBase enemy = Object.FindAnyObjectByType<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(1f, Vector2.right);
                enemy.ApplyKnockback(Vector2.right, 0.5f);
                enemy.MakeElite();
            }
        }

        private static List<EnemyBase> ExerciseEnemyCatalog(Vector3 playerPosition)
        {
            PoolManager pool = PoolManager.Instance;
            GameObject[] prefabs = Resources.LoadAll<GameObject>(
                "Brocoli/CursedDevolpmentStudioAss Assets/Waves"
            );
            var enemies = new List<EnemyBase>();
            for (int index = 0; index < prefabs.Length; index++)
            {
                EnemyBase prefab = prefabs[index].GetComponent<EnemyBase>();
                if (prefab == null)
                    continue;

                Vector3 position = playerPosition + new Vector3(0.3f + index * 0.03f, 0f, 0.3f);
                EnemyBase enemy = pool.GetEnemy(prefab, position, Quaternion.identity);
                Assert.That(enemy, Is.Not.Null, prefabs[index].name);
                ExerciseEnemyCombat(enemy);
                enemy.TakeDamage(1f, Vector2.right);
                enemy.ApplyKnockback(Vector2.right, 0.5f);
                enemy.MakeElite();
                if (enemy is HydraEnemyScript hydra)
                {
                    hydra.ConfigureForDungeonRing(index);
                    HydraEnemyScript.ExtraSplitGenerationsForRing(index);
                    HydraEnemyScript.RootScaleMultiplierForExtraSplits(index, 2);
                    HydraEnemyScript.ChildSpeedForScale(2f, 1f, 3f);
                }
                enemies.Add(enemy);
            }

            ExpGain experience = pool.GetExpGain(playerPosition + Vector3.right);
            if (experience != null)
                pool.ReturnExpGain(experience);

            foreach (
                string path in new[]
                {
                    "Brocoli/CursedDevolpmentStudioAss Assets/FireBall",
                    "Brocoli/CursedDevolpmentStudioAss Assets/FireBallBig",
                    "Brocoli/CursedDevolpmentStudioAss Assets/MiniCoronaProjectile",
                }
            )
            {
                GameObject asset = Resources.Load<GameObject>(path);
                EnemyProjectile prefab =
                    asset == null ? null : asset.GetComponent<EnemyProjectile>();
                if (prefab == null)
                    continue;
                EnemyProjectile projectile = pool.GetProjectile(
                    prefab,
                    playerPosition + Vector3.up,
                    Quaternion.identity
                );
                SetHierarchyField(projectile, "initialScale", Vector3.zero);
                projectile.Init(Vector2.right);
                SetHierarchyField(projectile, "spawnTime", Time.time - projectile.lifeTime);
                InvokeHierarchy(projectile, "Update");
                SetHierarchyField(projectile, "travelDirection", Vector2.zero);
                InvokeHierarchy(projectile, "FixedUpdate");
                pool.ReturnProjectile(projectile);
            }

            ExerciseEnemyProjectileWallSweep(playerPosition);

            ExerciseEnemyVariants(playerPosition, enemies);

            return enemies;
        }
    }
}
