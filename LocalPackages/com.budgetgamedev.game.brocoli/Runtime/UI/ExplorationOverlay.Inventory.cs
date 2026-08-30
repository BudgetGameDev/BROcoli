using System.Collections.Generic;
using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ExplorationOverlay
    {
        private static readonly Color InventorySurface = new(0.045f, 0.08f, 0.06f, 0.7f);
        private static readonly Color InventorySlot = new(0.12f, 0.18f, 0.14f, 0.62f);
        private static readonly Color OccupiedSlot = new(0.21f, 0.28f, 0.18f, 0.72f);
        private static readonly Color GearAccent = new(0.79f, 0.62f, 0.25f, 0.88f);

        private readonly List<RectTransform> nearbyRows = new();
        private readonly List<RectTransform> backpackSlots = new();
        private readonly List<TMP_Text> backpackLabels = new();
        private readonly List<RectTransform> gearSlots = new();
        private readonly List<RectTransform> selectedItemStatCells = new();
        private readonly List<TMP_Text> selectedItemStatLabels = new();

        private RectTransform nearbySurface;
        private RectTransform loadoutSurface;
        private RectTransform backpackSurface;
        private RectTransform gearStage;
        private RectTransform playerSilhouette;
        private RectTransform statsSurface;
        private TMP_Text nearbyTitle;
        private TMP_Text nearbyHint;
        private TMP_Text loadoutTitle;
        private TMP_Text loadoutHint;
        private TMP_Text statsTitle;
        private TMP_Text runSummary;
        private TMP_Text backpackTitle;
        private TMP_Text backpackHint;
        private TMP_Text statsLeft;
        private TMP_Text statsRight;
        private RectTransform selectedItemStatsSurface;
        private TMP_Text selectedItemTitle;
        private TMP_Text selectedItemHint;
        private TMP_Text inventoryDisclaimer;

        private static readonly string[] NearbyPreviewItems =
        {
            "SANITIZER REFILL",
            "RUBBER GLOVES",
            "MOSSY CHARM",
            "CLEANING TONIC",
            "SEALED RELIC",
            "DUSTY BOOTS",
            "SPARE FILTER",
            "COPPER BUCKLE",
            "HERBAL SOAP",
            "CRACKED LANTERN",
            "CLOTH WRAP",
            "ODD KEY",
            "EMPTY FLASK",
            "WORN BADGE",
        };

        private static readonly string[] BackpackPreview = { "REFILL", "TONIC", "CHARM" };

        private void BuildInventoryInterface()
        {
            InitializeMockInventory();
            BuildNearbyInterface();

            loadoutSurface = CreateInventorySurface("LoadoutSurface", inventoryPanel);
            loadoutTitle = CreateInventoryHeading("LoadoutTitle", loadoutSurface, "LOADOUT");
            loadoutHint = CreateInventoryHint(
                "LoadoutHint",
                loadoutSurface,
                "EQUIPPED GEAR  ·  PREVIEW"
            );

            gearStage = CreatePanel(
                "GearStage",
                loadoutSurface,
                new Color(0.03f, 0.055f, 0.04f, 0.42f)
            );
            SetGraphicRaycast(gearStage, false);
            AddInventoryOutline(
                gearStage.gameObject,
                new Color(GearAccent.r, GearAccent.g, GearAccent.b, 0.35f)
            );
            BuildPlayerSilhouette();
            for (int i = 0; i < gearItems.Length; i++)
                gearSlots.Add(CreateGearSlot(gearStage, i, GearLabel(i)));

            statsSurface = CreateInventorySurface("PlayerStats", inventoryPanel);
            statsTitle = CreateInventoryHeading("StatsHeading", statsSurface, "PLAYER STATS");
            runSummary = CreateInventoryHint("RunSummary", statsSurface, string.Empty);
            statsLeft = CreateStatColumn("StatsLeft", statsSurface);
            statsRight = CreateStatColumn("StatsRight", statsSurface);
            BuildSelectedItemStats();

            backpackSurface = CreateInventorySurface("BackpackSurface", inventoryPanel);
            backpackTitle = CreateInventoryHeading("BackpackTitle", backpackSurface, "BACKPACK");
            backpackHint = CreateInventoryHint(
                "BackpackHint",
                backpackSurface,
                "INVENTORY GRID  ·  PREVIEW"
            );
            for (int i = 0; i < backpackItems.Length; i++)
                backpackSlots.Add(CreateBackpackSlot(backpackSurface, i));

            inventoryDisclaimer = CreateText(
                "InventoryDisclaimer",
                inventoryPanel,
                DefaultInventoryInteractionHint,
                11f,
                OnSurfaceMuted,
                TMP_Settings.defaultFontAsset
            );
            inventoryDisclaimer.characterSpacing = 0.8f;
            inventoryDisclaimer.raycastTarget = false;
            BuildInventoryActionButtons();
            RefreshMockInventoryVisuals();
        }

        private static RectTransform CreateInventorySurface(string objectName, RectTransform parent)
        {
            RectTransform surface = CreatePanel(objectName, parent, InventorySurface);
            SetGraphicRaycast(surface, false);
            AddInventoryOutline(surface.gameObject, new Color(1f, 1f, 1f, 0.16f));
            return surface;
        }

        private static TMP_Text CreateInventoryHeading(
            string objectName,
            RectTransform parent,
            string value
        )
        {
            TMP_Text text = CreateText(
                objectName,
                parent,
                value,
                19f,
                OnSurface,
                TMP_Settings.defaultFontAsset
            );
            text.alignment = TextAlignmentOptions.Left;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 1.4f;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_Text CreateInventoryHint(
            string objectName,
            RectTransform parent,
            string value
        )
        {
            TMP_Text text = CreateText(
                objectName,
                parent,
                value,
                10f,
                OnSurfaceMuted,
                TMP_Settings.defaultFontAsset
            );
            text.alignment = TextAlignmentOptions.Left;
            text.characterSpacing = 0.7f;
            text.raycastTarget = false;
            return text;
        }

        private void BuildPlayerSilhouette()
        {
            playerSilhouette = CreatePanel(
                "PlayerSilhouette",
                gearStage,
                new Color(0.17f, 0.25f, 0.19f, 0.55f)
            );
            SetGraphicRaycast(playerSilhouette, false);
            AddInventoryOutline(playerSilhouette.gameObject, new Color(1f, 1f, 1f, 0.12f));

            TMP_Text player = CreateText(
                "PlayerMark",
                playerSilhouette,
                "B",
                56f,
                new Color(GearAccent.r, GearAccent.g, GearAccent.b, 0.52f),
                TMP_Settings.defaultFontAsset
            );
            Stretch(player.rectTransform);
            player.fontStyle = FontStyles.Bold;
            player.raycastTarget = false;
        }

        private RectTransform CreateGearSlot(RectTransform parent, int index, string labelValue)
        {
            bool occupied =
                labelValue.Contains("GLOVES")
                || labelValue.Contains("CHARM")
                || labelValue.Contains("SANITIZER");
            RectTransform slot = CreatePanel(
                $"GearSlot{index + 1:00}",
                parent,
                occupied ? OccupiedSlot : InventorySlot
            );
            AddInventoryOutline(
                slot.gameObject,
                occupied
                    ? new Color(GearAccent.r, GearAccent.g, GearAccent.b, 0.65f)
                    : new Color(1f, 1f, 1f, 0.14f)
            );
            RegisterInventoryItem(slot, InventoryPreviewLocation.Gear, index);

            TMP_Text label = CreateText(
                "Label",
                slot,
                labelValue,
                9f,
                occupied ? OnSurface : OnSurfaceMuted,
                TMP_Settings.defaultFontAsset
            );
            Stretch(label.rectTransform);
            label.fontStyle = occupied ? FontStyles.Bold : FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return slot;
        }
    }
}
