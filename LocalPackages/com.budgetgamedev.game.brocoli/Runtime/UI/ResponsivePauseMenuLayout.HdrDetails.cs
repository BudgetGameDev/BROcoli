using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsivePauseMenuLayout
    {
        private RectTransform hdrDetailsPanel;
        private TMP_Text hdrDetailsExplanation;
        private TMP_Text hdrDetailsValues;
        private Button hdrProfileButton;
        private Button hdrEdidButton;
        private Button hdrCalibrateButton;
        private Button hdrBackButton;
        private Button[] hdrDetailButtons;
        private Selectable[] hdrDetailSelectables;
        private DisplayEdidMetadata hdrEdidMetadata;
        private int selectedHdrDetail;
        private float lastHdrDetailNavTime;

        public bool HdrDetailsOpen { get; private set; }

        private void BuildHdrPresentation()
        {
            hdrDetailsPanel = CreateRect("PauseHdrDetailsPanel", card);
            hdrDetailsExplanation = CreateText(
                "HdrDetailsExplanation",
                hdrDetailsPanel,
                "EDID is the monitor's hardware report. The OS HDR profile includes calibration "
                    + "and the SDR-white preference used as paper white. EDID does not define paper white.",
                13f,
                OnSurfaceMuted,
                materialFont
            );
            hdrDetailsExplanation.textWrappingMode = TextWrappingModes.Normal;
            hdrDetailsExplanation.alignment = TextAlignmentOptions.Center;
            hdrDetailsValues = CreateText(
                "HdrDetailsValues",
                hdrDetailsPanel,
                string.Empty,
                14f,
                OnSurface,
                materialFont
            );
            hdrDetailsValues.textWrappingMode = TextWrappingModes.Normal;
            hdrDetailsValues.alignment = TextAlignmentOptions.TopLeft;
            hdrDetailsValues.richText = true;

            hdrProfileButton = CreateButton(
                "PauseHdrProfileButton",
                hdrDetailsPanel,
                "RESET TO HDR PROFILE"
            );
            hdrProfileButton.onClick.AddListener(UseHdrProfile);
            hdrEdidButton = CreateButton("PauseHdrEdidButton", hdrDetailsPanel, "RESET TO EDID");
            hdrEdidButton.onClick.AddListener(UseHdrEdid);
            hdrCalibrateButton = CreateButton(
                "PauseHdrCalibrateButton",
                hdrDetailsPanel,
                "CALIBRATE"
            );
            hdrCalibrateButton.onClick.AddListener(OpenHdrCalibration);
            hdrBackButton = CreateButton("PauseHdrBackButton", hdrDetailsPanel, "BACK");
            hdrBackButton.onClick.AddListener(HideHdrDetails);
            hdrDetailButtons = new[]
            {
                hdrProfileButton,
                hdrEdidButton,
                hdrCalibrateButton,
                hdrBackButton,
            };
            hdrDetailSelectables = new Selectable[]
            {
                hdrProfileButton,
                hdrEdidButton,
                hdrCalibrateButton,
                hdrBackButton,
            };
            foreach (Button button in hdrDetailButtons)
                StyleButton(button, false, materialFont);
            RegisterHdrDetailPointerSelection();
            hdrDetailsPanel.gameObject.SetActive(false);
            BuildHdrCalibrationPresentation();
        }

        private void ShowHdrDetails()
        {
            ProceduralUIAudio.PlaySelect();
            hdrEdidMetadata = DisplayEdidMetadata.Detect(
                GameDisplaySettings.HasDetectedHdrProfile
                    ? GameDisplaySettings.DetectedPeakBrightnessNits
                    : GameDisplaySettings.PeakBrightnessNits
            );
            HdrDetailsOpen = true;
            settingsPanel.gameObject.SetActive(false);
            hdrDetailsPanel.gameObject.SetActive(true);
            title.text = "HDR OUTPUT";
            footer.text = "ESC  ·  B  TO SETTINGS";
            SyncPauseHdrDetails();
            SelectHdrDetail(0, false);
            ApplyResponsiveLayout(true);
        }

        private void HideHdrDetails()
        {
            if (!HdrDetailsOpen)
                return;
            ProceduralUIAudio.PlaySelect();
            HdrDetailsOpen = false;
            hdrDetailsPanel.gameObject.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
            title.text = "SETTINGS";
            footer.text = "ESC  ·  B  TO PAUSE MENU";
            SyncSettings();
            SelectSetting(System.Array.IndexOf(settingsSelectables, hdrDetailsButton), false);
            ApplyResponsiveLayout(true);
        }

        private void UseHdrProfile()
        {
            if (!GameDisplaySettings.HasDetectedHdrProfile)
                return;
            ProceduralUIAudio.PlaySelect();
            GameDisplaySettings.ResetToDetectedHdrProfile();
            SyncPauseHdrDetails();
        }

        private void UseHdrEdid()
        {
            if (!hdrEdidMetadata.HasMaximumLuminance)
                return;
            ProceduralUIAudio.PlaySelect();
            float paperWhite = GameDisplaySettings.HasDetectedHdrProfile
                ? GameDisplaySettings.DetectedPaperWhiteNits
                : GameDisplaySettings.PaperWhiteNits;
            float minimum =
                hdrEdidMetadata.HasMinimumLuminance ? hdrEdidMetadata.MinimumLuminanceNits
                : GameDisplaySettings.HasDetectedHdrProfile
                    ? GameDisplaySettings.DetectedBlackLevelNits
                : GameDisplaySettings.BlackLevelNits;
            GameDisplaySettings.SetCalibration(
                hdrEdidMetadata.MaximumLuminanceNits,
                paperWhite,
                minimum
            );
            SyncPauseHdrDetails();
        }

        private void SyncPauseHdrDetails()
        {
            hdrProfileButton.interactable = GameDisplaySettings.HasDetectedHdrProfile;
            hdrEdidButton.interactable = hdrEdidMetadata.HasMaximumLuminance;
            string display = string.IsNullOrEmpty(hdrEdidMetadata.DisplayName)
                ? "DISPLAY NOT IDENTIFIED"
                : hdrEdidMetadata.DisplayName.ToUpperInvariant();
            hdrDetailsValues.text =
                $"<b><color=#9AE6B4>RAW DISPLAY METADATA (EDID)</color></b>  ·  {display}\n"
                + $"MINIMUM  {FormatOptionalNits(hdrEdidMetadata.HasMinimumLuminance, hdrEdidMetadata.MinimumLuminanceNits, true)}"
                + $"     PEAK  {FormatOptionalNits(hdrEdidMetadata.HasMaximumLuminance, hdrEdidMetadata.MaximumLuminanceNits, false)}\n"
                + $"FULL FRAME  {FormatOptionalNits(hdrEdidMetadata.HasMaximumFullFrameLuminance, hdrEdidMetadata.MaximumFullFrameLuminanceNits, false)}"
                + "     PAPER WHITE  NOT PROVIDED\n\n"
                + "<b><color=#9AE6B4>OS HDR PROFILE</color></b>\n"
                + (
                    GameDisplaySettings.HasDetectedHdrProfile
                        ? $"MINIMUM  {FormatNits(GameDisplaySettings.DetectedBlackLevelNits, true)}"
                            + $"     PEAK  {FormatNits(GameDisplaySettings.DetectedPeakBrightnessNits, false)}\n"
                            + $"FULL FRAME  {FormatNits(GameDisplaySettings.DetectedFullFrameBrightnessNits, false)}"
                            + $"     PAPER WHITE  {FormatNits(GameDisplaySettings.DetectedPaperWhiteNits, false)}"
                        : "NO ACTIVE HDR PROFILE REPORTED BY THE OS"
                )
                + "\n\n"
                + $"<b><color=#9AE6B4>CURRENT GAME OUTPUT  ·  {ResolvePauseHdrSource()}</color></b>\n"
                + $"MINIMUM  {FormatNits(GameDisplaySettings.BlackLevelNits, true)}"
                + $"     PEAK  {FormatNits(GameDisplaySettings.PeakBrightnessNits, false)}\n"
                + $"PAPER WHITE  {FormatNits(GameDisplaySettings.PaperWhiteNits, false)}";
        }

        private string ResolvePauseHdrSource()
        {
            if (GameDisplaySettings.UsingSystemCalibrationDefaults)
                return "HDR PROFILE";
            if (
                hdrEdidMetadata.HasMaximumLuminance
                && ApproximatelyNits(
                    GameDisplaySettings.PeakBrightnessNits,
                    hdrEdidMetadata.MaximumLuminanceNits
                )
                && (
                    !hdrEdidMetadata.HasMinimumLuminance
                    || ApproximatelyNits(
                        GameDisplaySettings.BlackLevelNits,
                        hdrEdidMetadata.MinimumLuminanceNits
                    )
                )
            )
                return "EDID METADATA";
            return "CUSTOM";
        }

        private static bool ApproximatelyNits(float first, float second) =>
            Mathf.Abs(first - second) <= Mathf.Max(0.001f, Mathf.Abs(second) * 0.0025f);

        private static string FormatOptionalNits(bool available, float value, bool precise) =>
            available ? FormatNits(value, precise) : "NOT REPORTED";

        private static string FormatNits(float value, bool precise) =>
            precise && value < 1f ? $"{value:0.0000} NITS" : $"{Mathf.RoundToInt(value)} NITS";

        private void RegisterHdrDetailPointerSelection()
        {
            for (int index = 0; index < hdrDetailSelectables.Length; index++)
            {
                int captured = index;
                EventTrigger trigger = hdrDetailSelectables[index]
                    .gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new() { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => selectedHdrDetail = captured);
                trigger.triggers.Add(entry);
            }
        }

        private void SelectHdrDetail(int index, bool sound = true)
        {
            selectedHdrDetail = (index + hdrDetailSelectables.Length) % hdrDetailSelectables.Length;
            Select(hdrDetailSelectables[selectedHdrDetail]);
            if (sound)
                ProceduralUIAudio.PlayHover();
        }
    }
}
