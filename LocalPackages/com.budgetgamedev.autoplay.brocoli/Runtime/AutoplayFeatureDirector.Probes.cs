using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class AutoplayFeatureDirector
    {
        private static readonly Vector2 InventoryStep = Vector2.right;
        private static readonly Vector2 MapPan = new(1.4f, -0.9f);

        private ExplorationOverlay overlay;
        private PauseMenu pauseMenu;
        private DungeonMapGraphic map;
        private Vector2 mapViewBeforePan;

        private ExplorationOverlay Overlay =>
            overlay != null ? overlay : overlay = ExplorationOverlay.EnsurePresent();

        private PauseMenu Pause =>
            pauseMenu != null ? pauseMenu : pauseMenu = FindAnyObjectByType<PauseMenu>();

        /// <summary>Only findable while the map pane is showing, which is when it is needed.</summary>
        private DungeonMapGraphic Map =>
            map != null ? map : map = FindAnyObjectByType<DungeonMapGraphic>();

        private void OpenInventory()
        {
            ExplorationOverlay open = Overlay;
            if (open == null || open.IsOpen)
                return;

            open.ProcessGlobalInput(false, true, false, false, false, false);
            if (open.IsOpen && open.ActivePane == ExplorationOverlay.Pane.Inventory)
                AutoplayFeatureLog.Record(AutoplayFeatures.InventoryOpened);
        }

        private void NavigateInventory()
        {
            ExplorationOverlay open = Overlay;
            if (open == null || !open.IsOpen)
                return;

            // Two moves with the repeat clock wound forward: the first claims a
            // selection, the second proves the selection can actually be moved.
            open.ProcessInventoryNavigation(InventoryStep, Time.unscaledTime);
            open.ProcessInventoryNavigation(Vector2.zero, Time.unscaledTime);
            open.ProcessInventoryNavigation(InventoryStep, Time.unscaledTime + 1f);
            if (open.SelectedInventoryItemName.Length > 0)
                AutoplayFeatureLog.Record(AutoplayFeatures.InventoryNavigated);
        }

        private void EquipInventoryItem()
        {
            ExplorationOverlay open = Overlay;
            if (open == null || !open.IsOpen || open.SelectedInventoryItemName.Length == 0)
                return;

            open.ProcessInventoryActions(false, true);
            AutoplayFeatureLog.Record(AutoplayFeatures.InventoryEquipped);
        }

        private void OpenMap()
        {
            ExplorationOverlay open = Overlay;
            if (open == null)
                return;

            open.ProcessGlobalInput(true, false, false, false, false, false);
            if (!open.IsOpen || open.ActivePane != ExplorationOverlay.Pane.Map)
                return;

            mapViewBeforePan = Map != null ? Map.ViewCenter : Vector2.zero;
            AutoplayFeatureLog.Record(AutoplayFeatures.MapOpened);
        }

        private void PanMap()
        {
            ExplorationOverlay open = Overlay;
            if (open == null || !open.IsOpen || open.ActivePane != ExplorationOverlay.Pane.Map)
                return;

            open.ProcessMapInput(MapPan, 0.5f, 1f);
            if (Map != null && Vector2.Distance(Map.ViewCenter, mapViewBeforePan) > 0.001f)
                AutoplayFeatureLog.Record(AutoplayFeatures.MapPanned);
        }

        private void CloseOverlay()
        {
            ExplorationOverlay open = Overlay;
            if (open != null && open.IsOpen)
                open.ProcessGlobalInput(false, false, false, true, false, false);
        }

        private void OpenPauseMenu()
        {
            PauseMenu menu = Pause;
            if (menu == null || menu.IsPaused())
                return;

            menu.TogglePause();
            if (menu.IsPaused())
                AutoplayFeatureLog.Record(AutoplayFeatures.PauseToggled);
        }

        private void OpenPauseSettings()
        {
            PauseMenu menu = Pause;
            if (menu == null || !menu.IsPaused() || menu.settingsButton == null)
                return;

            menu.settingsButton.onClick.Invoke();
            if (menu.SettingsOpen)
                AutoplayFeatureLog.Record(AutoplayFeatures.PauseSettings);
        }

        /// <summary>Resume also closes the settings pane, which is the path a player takes.</summary>
        private void ResumeFromPause()
        {
            PauseMenu menu = Pause;
            if (menu != null && menu.IsPaused())
                menu.Resume();
        }

        private static void ProbeSaveRoundTrip()
        {
            if (AutoplaySaveProbe.TryRoundTrip())
                AutoplayFeatureLog.Record(AutoplayFeatures.SaveRoundTrip);
        }
    }
}
