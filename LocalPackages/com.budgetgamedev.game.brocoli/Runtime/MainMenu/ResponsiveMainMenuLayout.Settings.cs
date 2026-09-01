using BudgetGameDev.Hub;
using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private RectTransform settingsPanel;
        private TMP_Text settingsTitle;
        private readonly Slider[] volumeSliders = new Slider[3];
        private readonly TMP_Text[] volumeValues = new TMP_Text[3];
        private readonly RectTransform[] volumeRows = new RectTransform[3];
        private readonly RectTransform[] settingsRows = new RectTransform[4];
        private RectTransform hdrRow;
        private Button hdrToggleButton;
        private TMP_Text hdrToggleValue;
        private Button hdrCalibrationButton;
        private TMP_Text hdrStatus;
        private Button settingsButton;
        private Button resetSettingsButton;
        private Button backSettingsButton;
        private Button launcherButton;
        private Button[] settingsActionButtons;
        private Selectable[] settingsSelectables;
        private bool[] mainButtonsWereActive;
        private int selectedSetting;
        private float lastSettingsNavTime;

        public static bool SettingsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSettingsState()
        {
            SettingsOpen = false;
            HdrCalibrationOpen = false;
            CreditsOpen = false;
            SavesOpen = false;
        }

        private void BuildSettingsPresentation()
        {
            // The play buttons the scene ships move into the save manager, which is
            // the only place a run is started or resumed.
            BuildSavesPresentation(mainButtons[0], mainButtons[1]);

            settingsButton = CreateButton("SettingsButton", card, "SETTINGS");
            settingsButton.onClick.AddListener(OpenSettings);
            BuildCreditsPresentation();

            // The hub owns game selection, so the way out of BROcoli is back to
            // the launcher rather than a quit that strands the player.
            launcherButton = CreateButton("LauncherButton", card, "ALL GAMES");
            launcherButton.onClick.AddListener(GameSession.ReturnToLauncher);

            mainButtons = new[]
            {
                savesButton,
                settingsButton,
                creditsButton,
                mainButtons[2],
                launcherButton,
                mainButtons[3],
            };

            settingsPanel = CreateRect("SettingsPanel", card);
            settingsTitle = CreateText(
                "SettingsTitle",
                settingsPanel,
                "SOUND & DISPLAY",
                22f,
                OnSurface
            );
            CreateVolumeRow(0, "MASTER", GameAudioSettings.SetMasterVolume);
            CreateVolumeRow(1, "AMBIENCE", GameAudioSettings.SetAmbienceVolume);
            CreateVolumeRow(2, "SOUND EFFECTS", GameAudioSettings.SetSfxVolume);
            CreateHdrRow();

            resetSettingsButton = CreateButton("ResetSettingsButton", settingsPanel, "RESET");
            resetSettingsButton.onClick.AddListener(ResetSettingsToDefaults);
            backSettingsButton = CreateButton("BackFromSettingsButton", settingsPanel, "BACK");
            backSettingsButton.onClick.AddListener(CloseSettings);
            settingsActionButtons = new[] { resetSettingsButton, backSettingsButton };
            settingsSelectables = new Selectable[]
            {
                volumeSliders[0],
                volumeSliders[1],
                volumeSliders[2],
                hdrToggleButton,
                hdrCalibrationButton,
                resetSettingsButton,
                backSettingsButton,
            };
            RegisterPointerSelection();
            SyncSettingsControls();
            GameAudioSettings.ValuesChanged += SyncVolumeControls;
            GameDisplaySettings.ValuesChanged += SyncHdrControl;
            settingsPanel.gameObject.SetActive(false);
            BuildHdrCalibrationPresentation();
        }

        private void OnDestroy()
        {
            GameAudioSettings.ValuesChanged -= SyncVolumeControls;
            GameDisplaySettings.ValuesChanged -= SyncHdrControl;
            DestroyHdrCalibrationMaterials();
            SettingsOpen = false;
            HdrCalibrationOpen = false;
            CreditsOpen = false;
            SavesOpen = false;
        }

        private void CreateVolumeRow(
            int index,
            string label,
            UnityEngine.Events.UnityAction<float> setter
        )
        {
            RectTransform row = CreateRect(label + "Row", settingsPanel);
            volumeRows[index] = row;
            settingsRows[index] = row;
            TMP_Text nameText = CreateText(label + "Label", row, label, 17f, OnSurfaceMuted);
            nameText.alignment = TextAlignmentOptions.Left;
            TMP_Text valueText = CreateText(label + "Value", row, "100%", 17f, OnSurface);
            valueText.alignment = TextAlignmentOptions.Right;
            volumeValues[index] = valueText;

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
            volumeSliders[index] = slider;
        }

        private void CreateHdrRow()
        {
            hdrRow = CreateRect("HdrOutputRow", settingsPanel);
            settingsRows[3] = hdrRow;

            TMP_Text nameText = CreateText(
                "HdrOutputLabel",
                hdrRow,
                "HDR OUTPUT",
                17f,
                OnSurfaceMuted
            );
            nameText.alignment = TextAlignmentOptions.Left;

            hdrToggleButton = CreateButton("HdrToggleButton", hdrRow, "ON");
            hdrToggleButton.onClick.AddListener(GameDisplaySettings.ToggleHdr);
            hdrToggleValue = hdrToggleButton.GetComponentInChildren<TMP_Text>(true);
            hdrCalibrationButton = CreateButton("HdrCalibrationButton", hdrRow, "CALIBRATE");
            hdrCalibrationButton.onClick.AddListener(OpenHdrCalibration);
            hdrStatus = CreateText("HdrStatus", hdrRow, string.Empty, 12f, OnSurfaceMuted);
            hdrStatus.alignment = TextAlignmentOptions.Left;
        }

        private static Button CreateButton(string name, RectTransform parent, string label)
        {
            RectTransform rect = CreatePanel(name, parent, Color.white);
            rect.GetComponent<Image>().raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            RectTransform labelRect = CreateRect("Label", rect);
            TextMeshProUGUI text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.raycastTarget = false;
            Stretch(labelRect);
            return button;
        }

        private void OpenSettings()
        {
            ProceduralUIAudio.PlaySelect();
            mainButtonsWereActive = new bool[mainButtons.Length];
            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] == null)
                    continue;
                mainButtonsWereActive[i] = mainButtons[i].gameObject.activeSelf;
                mainButtons[i].gameObject.SetActive(false);
            }

            SettingsOpen = true;
            settingsPanel.gameObject.SetActive(true);
            SyncSettingsControls();
            SelectSetting(0, false);
            ApplyResponsiveLayout(true);
        }

        private void CloseSettings()
        {
            ProceduralUIAudio.PlaySelect();
            if (HdrCalibrationOpen)
                CloseHdrCalibration(false);
            SettingsOpen = false;
            settingsPanel.gameObject.SetActive(false);
            if (mainButtonsWereActive != null)
            {
                for (int i = 0; i < mainButtons.Length; i++)
                    if (mainButtons[i] != null)
                        mainButtons[i].gameObject.SetActive(mainButtonsWereActive[i]);
            }

            GetComponent<MainMenu>()?.SetupControllerNavigation(true, settingsButton);
            ApplyResponsiveLayout(true);
        }

        private void SyncVolumeControls()
        {
            float[] values =
            {
                GameAudioSettings.MasterVolume,
                GameAudioSettings.AmbienceVolume,
                GameAudioSettings.SfxVolume,
            };
            for (int i = 0; i < volumeSliders.Length; i++)
            {
                volumeSliders[i].SetValueWithoutNotify(values[i]);
                volumeValues[i].text = $"{Mathf.RoundToInt(values[i] * 100f)}%";
            }
        }

        private void SyncSettingsControls()
        {
            SyncVolumeControls();
            SyncHdrControl();
        }

        private void SyncHdrControl()
        {
            if (hdrToggleValue != null)
                hdrToggleValue.text = GameDisplaySettings.HdrEnabled ? "ON" : "OFF";
            if (hdrStatus != null)
                hdrStatus.text = GameDisplaySettings.HdrStatus;
        }

        private static void ResetSettingsToDefaults()
        {
            GameAudioSettings.ResetToDefaults();
            GameDisplaySettings.ResetToDefault();
        }

        private void RegisterPointerSelection()
        {
            for (int i = 0; i < settingsSelectables.Length; i++)
            {
                int index = i;
                EventTrigger trigger = settingsSelectables[i]
                    .gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new() { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => selectedSetting = index);
                trigger.triggers.Add(entry);
            }
        }

        private void SelectSetting(int index, bool playSound = true)
        {
            selectedSetting = (index + settingsSelectables.Length) % settingsSelectables.Length;
            EventSystem.current?.SetSelectedGameObject(
                settingsSelectables[selectedSetting].gameObject
            );
            if (playSound)
                ProceduralUIAudio.PlayHover();
        }
    }
}
