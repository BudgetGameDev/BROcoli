using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed partial class ResponsiveMainMenuLayout
{
    private RectTransform settingsPanel;
    private TMP_Text settingsTitle;
    private readonly Slider[] volumeSliders = new Slider[3];
    private readonly TMP_Text[] volumeValues = new TMP_Text[3];
    private readonly RectTransform[] volumeRows = new RectTransform[3];
    private Button settingsButton;
    private Button resetSettingsButton;
    private Button backSettingsButton;
    private Button[] settingsActionButtons;
    private Selectable[] settingsSelectables;
    private bool[] mainButtonsWereActive;
    private int selectedSetting;
    private float lastSettingsNavTime;

    public static bool SettingsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSettingsState() => SettingsOpen = false;

    private void BuildSettingsPresentation()
    {
        settingsButton = CreateButton("SettingsButton", card, "SETTINGS");
        settingsButton.onClick.AddListener(OpenSettings);
        mainButtons = new[]
        {
            mainButtons[0],
            mainButtons[1],
            settingsButton,
            mainButtons[2],
            mainButtons[3],
        };
        GetComponent<GameModeMenu>()?.RegisterMainButton(settingsButton.gameObject);

        settingsPanel = CreateRect("SettingsPanel", card);
        settingsTitle = CreateText(
            "SettingsTitle",
            settingsPanel,
            "SOUND SETTINGS",
            22f,
            OnSurface
        );
        CreateVolumeRow(0, "MASTER", GameAudioSettings.SetMasterVolume);
        CreateVolumeRow(1, "AMBIENCE", GameAudioSettings.SetAmbienceVolume);
        CreateVolumeRow(2, "SOUND EFFECTS", GameAudioSettings.SetSfxVolume);

        resetSettingsButton = CreateButton("ResetAudioButton", settingsPanel, "RESET");
        resetSettingsButton.onClick.AddListener(GameAudioSettings.ResetToDefaults);
        backSettingsButton = CreateButton("BackFromSettingsButton", settingsPanel, "BACK");
        backSettingsButton.onClick.AddListener(CloseSettings);
        settingsActionButtons = new[] { resetSettingsButton, backSettingsButton };
        settingsSelectables = new Selectable[]
        {
            volumeSliders[0],
            volumeSliders[1],
            volumeSliders[2],
            resetSettingsButton,
            backSettingsButton,
        };
        RegisterPointerSelection();
        SyncVolumeControls();
        GameAudioSettings.ValuesChanged += SyncVolumeControls;
        settingsPanel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        GameAudioSettings.ValuesChanged -= SyncVolumeControls;
        SettingsOpen = false;
    }

    private void CreateVolumeRow(
        int index,
        string label,
        UnityEngine.Events.UnityAction<float> setter
    )
    {
        RectTransform row = CreateRect(label + "Row", settingsPanel);
        volumeRows[index] = row;
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
        SyncVolumeControls();
        SelectSetting(0, false);
        ApplyResponsiveLayout(true);
    }

    private void CloseSettings()
    {
        ProceduralUIAudio.PlaySelect();
        SettingsOpen = false;
        settingsPanel.gameObject.SetActive(false);
        if (mainButtonsWereActive != null)
        {
            for (int i = 0; i < mainButtons.Length; i++)
                if (mainButtons[i] != null)
                    mainButtons[i].gameObject.SetActive(mainButtonsWereActive[i]);
        }

        GetComponent<MainMenu>()?.SetupControllerNavigation(true);
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

    private void RegisterPointerSelection()
    {
        for (int i = 0; i < settingsSelectables.Length; i++)
        {
            int index = i;
            EventTrigger trigger = settingsSelectables[i].gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry entry = new() { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => selectedSetting = index);
            trigger.triggers.Add(entry);
        }
    }

    private void SelectSetting(int index, bool playSound = true)
    {
        selectedSetting = (index + settingsSelectables.Length) % settingsSelectables.Length;
        EventSystem.current?.SetSelectedGameObject(settingsSelectables[selectedSetting].gameObject);
        if (playSound)
            ProceduralUIAudio.PlayHover();
    }

    private void Update()
    {
        if (!SettingsOpen)
            return;

        if (
            Input.GetKeyDown(KeyCode.Escape)
            || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        )
        {
            CloseSettings();
            return;
        }

        float vertical =
            Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1f
            : Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? -1f
            : 0f;
        float horizontal =
            Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1f
            : Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? -1f
            : 0f;
        if (Gamepad.current != null)
        {
            Vector2 axis = Gamepad.current.dpad.ReadValue();
            if (axis.sqrMagnitude < 0.25f)
                axis = Gamepad.current.leftStick.ReadValue();
            if (Mathf.Abs(axis.y) > 0.5f)
                vertical = Mathf.Sign(axis.y);
            if (Mathf.Abs(axis.x) > 0.5f)
                horizontal = Mathf.Sign(axis.x);
        }

        if (Time.unscaledTime - lastSettingsNavTime >= 0.18f)
        {
            if (Mathf.Abs(vertical) > 0.5f)
            {
                lastSettingsNavTime = Time.unscaledTime;
                SelectSetting(selectedSetting + (vertical > 0f ? -1 : 1));
            }
            else if (Mathf.Abs(horizontal) > 0.5f && selectedSetting < volumeSliders.Length)
            {
                lastSettingsNavTime = Time.unscaledTime;
                volumeSliders[selectedSetting].value += Mathf.Sign(horizontal) * 0.05f;
            }
        }

        bool submit =
            Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.Space)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        if (submit && settingsSelectables[selectedSetting] is Button button)
            button.onClick.Invoke();
    }
}
