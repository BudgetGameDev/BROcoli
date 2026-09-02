using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private RectTransform hdrDetailsPanel;
        private TMP_Text hdrDetailsTitle;
        private TMP_Text hdrDetailsSubtitle;
        private TMP_Text hdrDetailsExplanation;
        private TMP_Text hdrDetailsValues;
        private Button hdrDetailsProfileButton;
        private Button hdrDetailsEdidButton;
        private Button hdrDetailsCalibrateButton;
        private Button hdrDetailsBackButton;
        private Button[] hdrDetailsActionButtons;
        private Selectable[] hdrDetailsSelectables;
        private DisplayEdidMetadata hdrEdidMetadata;
        private int selectedHdrDetailsControl;
        private float lastHdrDetailsNavTime;
        private bool hdrCalibrationReturnToDetails;

        public static bool HdrDetailsOpen { get; private set; }

        private void OpenHdrDetails()
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
            SyncHdrDetails();
            SelectHdrDetailsControl(0, false);
            ApplyResponsiveLayout(true);
        }

        private void CloseHdrDetails()
        {
            if (!HdrDetailsOpen)
                return;

            ProceduralUIAudio.PlaySelect();
            DismissHdrDetailsPanel();
        }

        private void DismissHdrDetailsPanel()
        {
            HdrDetailsOpen = false;
            hdrDetailsPanel.gameObject.SetActive(false);
            if (SettingsOpen)
            {
                settingsPanel.gameObject.SetActive(true);
                SyncHdrControl();
                int index = System.Array.IndexOf(settingsSelectables, hdrCalibrationButton);
                SelectSetting(Mathf.Max(0, index), false);
            }
            ApplyResponsiveLayout(true);
        }

        private void UseHdrProfileValues()
        {
            if (!GameDisplaySettings.HasDetectedHdrProfile)
                return;

            ProceduralUIAudio.PlaySelect();
            GameDisplaySettings.ResetToDetectedHdrProfile();
            SyncHdrDetails();
        }

        private void UseEdidValues()
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
            SyncHdrDetails();
        }

        private void SyncHdrDetails()
        {
            if (hdrDetailsValues == null)
                return;

            hdrDetailsProfileButton.interactable = GameDisplaySettings.HasDetectedHdrProfile;
            hdrDetailsEdidButton.interactable = hdrEdidMetadata.HasMaximumLuminance;
            string edidName = string.IsNullOrEmpty(hdrEdidMetadata.DisplayName)
                ? "DISPLAY NOT IDENTIFIED"
                : hdrEdidMetadata.DisplayName.ToUpperInvariant();
            string source = ResolveCurrentHdrSource();
            hdrDetailsValues.text =
                $"<b><color=#9AE6B4>RAW DISPLAY METADATA (EDID)</color></b>  ·  {edidName}\n"
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
                + $"<b><color=#9AE6B4>CURRENT GAME OUTPUT  ·  {source}</color></b>\n"
                + $"MINIMUM  {FormatNits(GameDisplaySettings.BlackLevelNits, true)}"
                + $"     PEAK  {FormatNits(GameDisplaySettings.PeakBrightnessNits, false)}\n"
                + $"PAPER WHITE  {FormatNits(GameDisplaySettings.PaperWhiteNits, false)}";
        }

        private string ResolveCurrentHdrSource()
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

        private void RegisterHdrDetailsPointerSelection()
        {
            for (int index = 0; index < hdrDetailsSelectables.Length; index++)
            {
                int capturedIndex = index;
                EventTrigger trigger = hdrDetailsSelectables[index]
                    .gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new() { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => selectedHdrDetailsControl = capturedIndex);
                trigger.triggers.Add(entry);
            }
        }

        private void SelectHdrDetailsControl(int index, bool playSound = true)
        {
            selectedHdrDetailsControl =
                (index + hdrDetailsSelectables.Length) % hdrDetailsSelectables.Length;
            EventSystem.current?.SetSelectedGameObject(
                hdrDetailsSelectables[selectedHdrDetailsControl].gameObject
            );
            if (playSound)
                ProceduralUIAudio.PlayHover();
        }
    }
}
