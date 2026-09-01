using System;
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
        private readonly Slider[] volumeSliders = new Slider[3];
        private readonly TMP_Text[] volumeValues = new TMP_Text[3];
        private readonly RectTransform[] settingsRows = new RectTransform[4];
        private RectTransform settingsPanel;
        private Button settingsButton;
        private Button hdrToggleButton;
        private TMP_Text hdrToggleValue;
        private Button hdrDetailsButton;
        private TMP_Text hdrStatus;
        private Button resetSettingsButton;
        private Button backSettingsButton;
        private Selectable[] settingsSelectables;
        private int selectedSetting;
        private float lastSettingsNavTime;
        private Action settingsClosed;

        public bool SettingsOpen { get; private set; }

        private void BuildSettingsPresentation()
        {
            settingsPanel = CreateRect("PauseSettingsPanel", card);
            CreateVolumeRow(0, "MASTER", GameAudioSettings.SetMasterVolume);
            CreateVolumeRow(1, "AMBIENCE", GameAudioSettings.SetAmbienceVolume);
            CreateVolumeRow(2, "SOUND EFFECTS", GameAudioSettings.SetSfxVolume);
            CreateHdrRow();
            resetSettingsButton = CreateButton("ResetPauseSettingsButton", settingsPanel, "RESET");
            resetSettingsButton.onClick.AddListener(ResetSettings);
            backSettingsButton = CreateButton("BackFromPauseSettingsButton", settingsPanel, "BACK");
            backSettingsButton.onClick.AddListener(HideSettings);
            settingsSelectables = new Selectable[]
            {
                volumeSliders[0],
                volumeSliders[1],
                volumeSliders[2],
                hdrToggleButton,
                hdrDetailsButton,
                resetSettingsButton,
                backSettingsButton,
            };
            RegisterSettingsPointerSelection();
            StyleButton(hdrToggleButton, false, materialFont);
            StyleButton(hdrDetailsButton, false, materialFont);
            StyleButton(resetSettingsButton, false, materialFont);
            StyleButton(backSettingsButton, false, materialFont);
            settingsPanel.gameObject.SetActive(false);
            BuildHdrPresentation();
        }

        private void CreateVolumeRow(
            int index,
            string label,
            UnityEngine.Events.UnityAction<float> setter
        )
        {
            RectTransform row = CreateRect(label + "PauseRow", settingsPanel);
            settingsRows[index] = row;
            TMP_Text name = CreateText(
                label + "Label",
                row,
                label,
                16f,
                OnSurfaceMuted,
                materialFont
            );
            name.alignment = TextAlignmentOptions.Left;
            TMP_Text value = CreateText(label + "Value", row, "100%", 16f, OnSurface, materialFont);
            value.alignment = TextAlignmentOptions.Right;
            volumeValues[index] = value;

            RectTransform track = CreatePanel("Track", row, Hex("#53645A"));
            track.GetComponent<Image>().raycastTarget = true;
            RectTransform fillArea = CreateRect("Fill Area", track);
            RectTransform fill = CreatePanel("Fill", fillArea, Primary);
            RectTransform handleArea = CreateRect("Handle Slide Area", track);
            RectTransform handle = CreatePanel("Handle", handleArea, OnSurface);
            handle.GetComponent<Image>().raycastTarget = true;
            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.onValueChanged.AddListener(setter);
            slider.onValueChanged.AddListener(value =>
                volumeValues[index].text = $"{Mathf.RoundToInt(value * 100f)}%"
            );
            volumeSliders[index] = slider;
        }

        private void CreateHdrRow()
        {
            RectTransform row = CreateRect("PauseHdrOutputRow", settingsPanel);
            settingsRows[3] = row;
            TMP_Text name = CreateText(
                "HdrOutputLabel",
                row,
                "HDR OUTPUT",
                16f,
                OnSurfaceMuted,
                materialFont
            );
            name.alignment = TextAlignmentOptions.Left;
            hdrToggleButton = CreateButton("PauseHdrToggleButton", row, "ON");
            hdrToggleButton.onClick.AddListener(GameDisplaySettings.ToggleHdr);
            hdrToggleButton.onClick.AddListener(SyncSettings);
            hdrToggleValue = hdrToggleButton.GetComponentInChildren<TMP_Text>(true);
            hdrDetailsButton = CreateButton("PauseHdrDetailsButton", row, "DETAILS");
            hdrDetailsButton.onClick.AddListener(ShowHdrDetails);
            hdrStatus = CreateText(
                "HdrStatus",
                row,
                string.Empty,
                11f,
                OnSurfaceMuted,
                materialFont
            );
            hdrStatus.alignment = TextAlignmentOptions.Left;
        }

        public void ShowSettings(Action onClosed)
        {
            settingsClosed = onClosed;
            SettingsOpen = true;
            foreach (Button button in buttons)
                if (button != null)
                    button.gameObject.SetActive(false);
            title.text = "SETTINGS";
            footer.text = "ESC  ·  B  TO PAUSE MENU";
            settingsPanel.gameObject.SetActive(true);
            SyncSettings();
            SelectSetting(0, false);
            ApplyResponsiveLayout(true);
        }

        public void HideSettings() => HideSettings(true);

        internal void HideSettings(bool playSound)
        {
            if (!SettingsOpen)
                return;
            if (playSound)
                ProceduralUIAudio.PlaySelect();
            HideAllSettingsPanels();
            SettingsOpen = false;
            foreach (Button button in buttons)
                if (button != null)
                    button.gameObject.SetActive(true);
            title.text = "PAUSED";
            footer.text = "ESC  ·  START  ·  B  TO RESUME";
            ApplyResponsiveLayout(true);
            Action callback = settingsClosed;
            settingsClosed = null;
            callback?.Invoke();
        }

        private void HideAllSettingsPanels()
        {
            if (HdrCalibrationOpen)
                EndHdrCalibration(false);
            HdrDetailsOpen = false;
            if (hdrDetailsPanel != null)
                hdrDetailsPanel.gameObject.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
        }

        private void ResetSettings()
        {
            ProceduralUIAudio.PlaySelect();
            GameAudioSettings.ResetToDefaults();
            GameDisplaySettings.ResetToDefault();
            SyncSettings();
        }

        private void SyncSettings()
        {
            float[] values =
            {
                GameAudioSettings.MasterVolume,
                GameAudioSettings.AmbienceVolume,
                GameAudioSettings.SfxVolume,
            };
            for (int index = 0; index < volumeSliders.Length; index++)
            {
                volumeSliders[index].SetValueWithoutNotify(values[index]);
                volumeValues[index].text = $"{Mathf.RoundToInt(values[index] * 100f)}%";
            }
            hdrToggleValue.text = GameDisplaySettings.HdrEnabled ? "ON" : "OFF";
            hdrStatus.text = GameDisplaySettings.HdrStatus;
        }

        private void RegisterSettingsPointerSelection()
        {
            for (int index = 0; index < settingsSelectables.Length; index++)
            {
                int captured = index;
                EventTrigger trigger = settingsSelectables[index]
                    .gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new() { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => selectedSetting = captured);
                trigger.triggers.Add(entry);
            }
        }

        private void SelectSetting(int index, bool sound = true)
        {
            selectedSetting = (index + settingsSelectables.Length) % settingsSelectables.Length;
            EventSystem.current?.SetSelectedGameObject(
                settingsSelectables[selectedSetting].gameObject
            );
            if (sound)
                ProceduralUIAudio.PlayHover();
        }
    }
}
