using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class UIRemainderCoverageTests
    {
        private const BindingFlags Hidden =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void InventoryCellCoversHoverExitAndSelectedHoverColor()
        {
            GameObject host = new(
                "Coverage inventory preview",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(InventoryPreviewItem)
            );
            try
            {
                Image image = host.GetComponent<Image>();
                InventoryPreviewItem item = host.GetComponent<InventoryPreviewItem>();
                item.Configure(null, image, InventoryPreviewLocation.Backpack, 2);
                item.SetSelected(true);
                item.OnPointerEnter(null);
                Assert.That(item.IsHovered, Is.True);
                item.OnPointerExit(null);
                Assert.That(item.IsHovered, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExplorationInventoryCoversEmptySelectionsAndFallbackLocations()
        {
            GameObject overlayHost = new("Coverage exploration remainder");
            GameObject itemHost = new(
                "Coverage exploration item",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(InventoryPreviewItem)
            );
            overlayHost.SetActive(false);
            try
            {
                ExplorationOverlay overlay = overlayHost.AddComponent<ExplorationOverlay>();
                Invoke(overlay, "TransferSelectedInventoryItem");
                Invoke(overlay, "EquipSelectedInventoryItem");

                InventoryPreviewItem item = itemHost.GetComponent<InventoryPreviewItem>();
                item.Configure(
                    overlay,
                    itemHost.GetComponent<Image>(),
                    (InventoryPreviewLocation)int.MaxValue,
                    0
                );
                Set(overlay, "selectedInventoryItem", item);
                Assert.That(Invoke(overlay, "SelectedItemName"), Is.Null);
                Assert.That(
                    (string)Invoke(overlay, "SelectedItemLocationLabel"),
                    Is.EqualTo("ITEM")
                );

                item.Configure(
                    overlay,
                    itemHost.GetComponent<Image>(),
                    InventoryPreviewLocation.Backpack,
                    99
                );
                Set(overlay, "backpackItems", new string[1]);
                Set(overlay, "gearItems", new string[1]);
                Invoke(overlay, "EquipSelectedInventoryItem");

                item.Configure(
                    overlay,
                    itemHost.GetComponent<Image>(),
                    InventoryPreviewLocation.Backpack,
                    0
                );
                Set(overlay, "backpackItems", new[] { "ITEM" });
                Set(overlay, "gearItems", new string[1]);
                Set(overlay, "activeGearSlotIndex", 0);
                Invoke(overlay, "EquipSelectedInventoryItem");

                item.Configure(
                    overlay,
                    itemHost.GetComponent<Image>(),
                    InventoryPreviewLocation.Gear,
                    0
                );
                Set(overlay, "gearItems", new[] { "ITEM" });
                Set(overlay, "backpackItems", null);
                Set(overlay, "nearbyItems", new System.Collections.Generic.List<string>());
                Invoke(overlay, "UnequipSelectedGearItem");

                Set(overlay, "nearbyItems", null);
                Invoke(overlay, "RefreshMockInventoryVisuals");
                Invoke(overlay, "RefreshNearbyList");
                Invoke(overlay, "UpdateNearbyRowsLayout");

                GameObject root = new("Coverage open overlay", typeof(RectTransform));
                Set(overlay, "overlayRoot", root.GetComponent<RectTransform>());
                overlay.ProcessGlobalInput(false, false, true, false, false, false);
                Assert.That(root.activeSelf, Is.False);
                UnityEngine.Object.DestroyImmediate(root);

                Assert.That(
                    ExplorationOverlay.ResolveVisibleOffset(10f, 2f, 3f, 5f),
                    Is.EqualTo(2f)
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(itemHost);
                UnityEngine.Object.DestroyImmediate(overlayHost);
            }
        }

        [Test]
        [TestMustExpectAllLogs(false)]
        public void OverlayAndCtaCoverMissingCanvasAndEarlyDisplayGuards()
        {
            foreach (
                Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            )
                UnityEngine.Object.DestroyImmediate(canvas.gameObject);

            Assert.That(ExplorationOverlay.EnsurePresent(), Is.Null);
            GameOverOverlay overlay = (GameOverOverlay)InvokeStatic(
                typeof(GameOverOverlay),
                "CreateOverlay"
            );
            Invoke(overlay, "Update");

            GameObject ctaHost = new("Coverage CTA remainder");
            ctaHost.SetActive(false);
            try
            {
                GameOverCTAManager cta = ctaHost.AddComponent<GameOverCTAManager>();
                PauseMenu pause = ctaHost.AddComponent<PauseMenu>();
                string loadedScene = null;
                pause.GoToMainMenu(scene => loadedScene = scene);
                Assert.That(loadedScene, Is.EqualTo("Brocoli_MainMenu"));
                Assert.That(PauseMenu.FindNamedButton(new Button[] { null }, "resume"), Is.Null);
                PlayerPrefs.SetInt("LastScore", -1);
                IEnumerator show = (IEnumerator)Invoke(cta, "ShowAfterOverlayLayout");
                Assert.That(show.MoveNext(), Is.True);
                Assert.That(show.MoveNext(), Is.False);

                UnityEngine.Object.DestroyImmediate(overlay.transform.root.gameObject);
                LogAssert.Expect(LogType.Warning, "[GameOverCTA] No canvas found for fallback UI.");
                Invoke(cta, "CreateFallbackUI");
            }
            finally
            {
                PlayerPrefs.DeleteKey("LastScore");
                UnityEngine.Object.DestroyImmediate(ctaHost);
            }
        }

        [Test]
        public void HudAndVignetteCoverDuplicateFallbackAndNullLayoutPaths()
        {
            GameObject firstHost = new("Coverage first HUD");
            GameObject secondHost = new("Coverage second HUD");
            GameObject enemyHost = new("Enemy");
            GameObject targetHost = new("Target Enemy");
            enemyHost.SetActive(false);
            targetHost.SetActive(false);
            try
            {
                firstHost.SetActive(false);
                secondHost.SetActive(false);
                DiabloHud first = firstHost.AddComponent<DiabloHud>();
                SetStatic(typeof(DiabloHud), "instance", first);
                secondHost.AddComponent<DiabloHud>();
                InvokeStatic(typeof(DiabloHud), "SetBottomCorner", null, false, 0f, 0f, 1f, 1f);

                enemyHost.AddComponent<BoxCollider>();
                enemyHost.AddComponent<Rigidbody>();
                EnemyScript enemy = enemyHost.AddComponent<EnemyScript>();
                Assert.That(
                    (string)InvokeStatic(typeof(DiabloHud), "FormatEnemyName", enemy),
                    Is.EqualTo("ENEMY")
                );
                targetHost.AddComponent<BoxCollider>();
                targetHost.AddComponent<Rigidbody>();
                EnemyScript targetEnemy = targetHost.AddComponent<EnemyScript>();
                targetEnemy.isElite = false;
                enemy.isElite = true;
                Set(first, "enemyTarget", targetEnemy);
                Set(first, "enemyTargetLockedUntil", float.MaxValue);
                Invoke(first, "ShowEnemy", enemy);

                GameObject vignetteHost = new("Coverage vignette remainder");
                DamageVignette vignette = vignetteHost.AddComponent<DamageVignette>();
                Set(vignette, "_vignetteImage", null);
                LogAssert.Expect(
                    LogType.Warning,
                    "DamageVignette: No Canvas found - creating overlay canvas"
                );
                vignette.TriggerPulse(0.5f);
                UnityEngine.Object.DestroyImmediate(vignetteHost);
            }
            finally
            {
                SetStatic(typeof(DiabloHud), "instance", null);
                UnityEngine.Object.DestroyImmediate(targetHost);
                UnityEngine.Object.DestroyImmediate(enemyHost);
                UnityEngine.Object.DestroyImmediate(secondHost);
                UnityEngine.Object.DestroyImmediate(firstHost);
            }
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
                foreach (MethodInfo method in type.GetMethods(Hidden))
                    if (method.Name == name && method.GetParameters().Length == arguments.Length)
                        return method.Invoke(target, arguments);
            throw new MissingMethodException(target.GetType().Name, name);
        }

        private static object InvokeStatic(Type type, string name, params object[] arguments)
        {
            foreach (MethodInfo method in type.GetMethods(Hidden))
                if (method.Name == name && method.GetParameters().Length == arguments.Length)
                    return method.Invoke(null, arguments);
            throw new MissingMethodException(type.Name, name);
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Hidden).SetValue(target, value);

        private static void SetStatic(Type type, string name, object value) =>
            type.GetField(name, Hidden).SetValue(null, value);
    }
}
