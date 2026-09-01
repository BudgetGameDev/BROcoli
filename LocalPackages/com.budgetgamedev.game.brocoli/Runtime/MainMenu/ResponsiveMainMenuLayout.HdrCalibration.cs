using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        internal enum HdrCalibrationStep
        {
            PeakBrightness,
            BlackLevel,
        }

        private RectTransform hdrCalibrationPanel;
        private TMP_Text hdrCalibrationTitle;
        private TMP_Text hdrCalibrationStepLabel;
        private TMP_Text hdrCalibrationInstructions;
        private TMP_Text hdrCalibrationValue;
        private RectTransform hdrCalibrationPreview;
        private Image hdrPreviewBackground;
        private Image hdrPreviewReference;
        private Image hdrPreviewMark;
        private Slider hdrCalibrationSlider;
        private Button hdrCalibrationSystemButton;
        private Button hdrCalibrationBackButton;
        private Button hdrCalibrationNextButton;
        private Button[] hdrCalibrationActionButtons;
        private Selectable[] hdrCalibrationSelectables;
        private Material[] hdrPreviewMaterials;
        private HdrCalibrationStep hdrCalibrationStep;
        private int selectedHdrCalibrationControl;
        private float lastHdrCalibrationNavTime;
        private bool suppressHdrCalibrationCallback;
        private bool initialHdrEnabled;
        private float pendingPeakBrightness;
        private float pendingBlackLevel;

        public static bool HdrCalibrationOpen { get; private set; }

        private void OpenHdrCalibration()
        {
            ProceduralUIAudio.PlaySelect();
            hdrCalibrationReturnToDetails = HdrDetailsOpen;
            initialHdrEnabled = GameDisplaySettings.HdrEnabled;
            pendingPeakBrightness = GameDisplaySettings.PeakBrightnessNits;
            pendingBlackLevel = GameDisplaySettings.BlackLevelNits;
            GameDisplaySettings.SetHdrEnabled(true);
            GameDisplaySettings.BeginHdrCalibrationPreview();

            HdrCalibrationOpen = true;
            HdrDetailsOpen = false;
            settingsPanel.gameObject.SetActive(false);
            hdrDetailsPanel.gameObject.SetActive(false);
            hdrCalibrationPanel.gameObject.SetActive(true);
            SetHdrCalibrationStep(HdrCalibrationStep.PeakBrightness);
            SelectHdrCalibrationControl(0, false);
            ApplyResponsiveLayout(true);
        }

        private void CloseHdrCalibration(bool save)
        {
            if (!HdrCalibrationOpen)
                return;

            GameDisplaySettings.EndHdrCalibrationPreview();
            if (save)
            {
                GameDisplaySettings.SetCalibration(
                    pendingPeakBrightness,
                    GameDisplaySettings.PaperWhiteNits,
                    pendingBlackLevel
                );
            }
            else
            {
                GameDisplaySettings.SetHdrEnabled(initialHdrEnabled);
            }

            DismissHdrCalibrationPanel();
        }

        private void ResetHdrCalibrationToSystem()
        {
            if (!HdrCalibrationOpen)
                return;

            ProceduralUIAudio.PlaySelect();
            GameDisplaySettings.EndHdrCalibrationPreview();
            GameDisplaySettings.ResetToSystemCalibration();
            DismissHdrCalibrationPanel();
        }

        private void DismissHdrCalibrationPanel()
        {
            HdrCalibrationOpen = false;
            hdrCalibrationPanel.gameObject.SetActive(false);
            if (SettingsOpen)
            {
                if (hdrCalibrationReturnToDetails)
                {
                    HdrDetailsOpen = true;
                    hdrDetailsPanel.gameObject.SetActive(true);
                    SyncHdrDetails();
                    SelectHdrDetailsControl(2, false);
                }
                else
                {
                    settingsPanel.gameObject.SetActive(true);
                    SyncHdrControl();
                    int index = System.Array.IndexOf(settingsSelectables, hdrCalibrationButton);
                    SelectSetting(Mathf.Max(0, index), false);
                }
            }
            hdrCalibrationReturnToDetails = false;
            ApplyResponsiveLayout(true);
        }

        private void PreviousHdrCalibrationStep()
        {
            ProceduralUIAudio.PlaySelect();
            if (hdrCalibrationStep == HdrCalibrationStep.PeakBrightness)
            {
                CloseHdrCalibration(false);
                return;
            }

            SetHdrCalibrationStep((HdrCalibrationStep)((int)hdrCalibrationStep - 1));
        }

        private void NextHdrCalibrationStep()
        {
            ProceduralUIAudio.PlaySelect();
            if (hdrCalibrationStep == HdrCalibrationStep.BlackLevel)
            {
                CloseHdrCalibration(true);
                return;
            }

            SetHdrCalibrationStep((HdrCalibrationStep)((int)hdrCalibrationStep + 1));
        }

        private void SetHdrCalibrationStep(HdrCalibrationStep step)
        {
            hdrCalibrationStep = step;
            suppressHdrCalibrationCallback = true;
            switch (step)
            {
                case HdrCalibrationStep.PeakBrightness:
                    hdrCalibrationStepLabel.text = "STEP 1 OF 2  •  MAXIMUM LUMINANCE";
                    hdrCalibrationInstructions.text =
                        "Sets the brightest highlight your display can reproduce. Move right "
                        + "until the center square disappears, then move left one step so it is "
                        + "barely visible.";
                    hdrCalibrationSlider.minValue = GameDisplaySettings.MinimumPeakBrightnessNits;
                    hdrCalibrationSlider.maxValue = GameDisplaySettings.MaximumPeakBrightnessNits;
                    hdrCalibrationSlider.wholeNumbers = true;
                    hdrCalibrationSlider.SetValueWithoutNotify(pendingPeakBrightness);
                    break;
                default:
                    hdrCalibrationStepLabel.text = "STEP 2 OF 2  •  MINIMUM LUMINANCE";
                    hdrCalibrationInstructions.text =
                        "Sets the darkest shadow your display can distinguish. Start at zero and "
                        + "move right until the center square is barely visible while the "
                        + "surrounding area still looks black.";
                    hdrCalibrationSlider.minValue = 0f;
                    hdrCalibrationSlider.maxValue = 1f;
                    hdrCalibrationSlider.wholeNumbers = false;
                    hdrCalibrationSlider.SetValueWithoutNotify(
                        BlackLevelToSlider(pendingBlackLevel)
                    );
                    break;
            }
            suppressHdrCalibrationCallback = false;
            hdrCalibrationBackButton.GetComponentInChildren<TMP_Text>(true).text =
                step == HdrCalibrationStep.PeakBrightness ? "CANCEL" : "BACK";
            hdrCalibrationNextButton.GetComponentInChildren<TMP_Text>(true).text =
                step == HdrCalibrationStep.BlackLevel ? "APPLY" : "NEXT";
            SyncHdrCalibrationPreview();
        }

        private void OnHdrCalibrationSliderChanged(float value)
        {
            if (suppressHdrCalibrationCallback)
                return;

            switch (hdrCalibrationStep)
            {
                case HdrCalibrationStep.PeakBrightness:
                    pendingPeakBrightness = Mathf.Clamp(
                        Mathf.Round(value / 25f) * 25f,
                        GameDisplaySettings.MinimumPeakBrightnessNits,
                        GameDisplaySettings.MaximumPeakBrightnessNits
                    );
                    break;
                default:
                    pendingBlackLevel = SliderToBlackLevel(value);
                    break;
            }
            suppressHdrCalibrationCallback = true;
            float sliderValue;
            switch (hdrCalibrationStep)
            {
                case HdrCalibrationStep.PeakBrightness:
                    sliderValue = pendingPeakBrightness;
                    break;
                default:
                    sliderValue = BlackLevelToSlider(pendingBlackLevel);
                    break;
            }
            hdrCalibrationSlider.SetValueWithoutNotify(sliderValue);
            suppressHdrCalibrationCallback = false;
            SyncHdrCalibrationPreview();
        }

        private void SyncHdrCalibrationPreview()
        {
            if (hdrCalibrationValue == null)
                return;

            float backgroundNits;
            float referenceNits;
            float markNits;
            switch (hdrCalibrationStep)
            {
                case HdrCalibrationStep.PeakBrightness:
                    hdrCalibrationValue.text = $"{Mathf.RoundToInt(pendingPeakBrightness)} NITS";
                    backgroundNits = 0f;
                    referenceNits = pendingPeakBrightness * 0.9f;
                    markNits = pendingPeakBrightness;
                    break;
                default:
                    hdrCalibrationValue.text = $"{pendingBlackLevel:0.0000} NITS";
                    backgroundNits = 0f;
                    referenceNits = 0f;
                    markNits = pendingBlackLevel;
                    break;
            }

            SetHdrPreviewLuminance(hdrPreviewBackground, backgroundNits);
            SetHdrPreviewLuminance(hdrPreviewReference, referenceNits);
            SetHdrPreviewLuminance(hdrPreviewMark, markNits);
        }

        private static void SetHdrPreviewLuminance(Image image, float nits)
        {
            if (image == null || image.material == null)
                return;
            float sceneValue = Mathf.Max(0f, nits / GameDisplaySettings.PaperWhiteNits);
            image.material.SetColor("_Color", new Color(sceneValue, sceneValue, sceneValue, 1f));
        }

        internal static float SliderToBlackLevel(float value)
        {
            if (value <= 0f)
                return 0f;
            const float minimumVisibleBlack = 0.0001f;
            const float zeroDetent = 0.01f;
            return Mathf.Pow(
                10f,
                Mathf.Lerp(
                    Mathf.Log10(minimumVisibleBlack),
                    Mathf.Log10(GameDisplaySettings.MaximumBlackLevelNits),
                    Mathf.InverseLerp(zeroDetent, 1f, value)
                )
            );
        }

        internal static float BlackLevelToSlider(float nits)
        {
            if (nits <= 0f)
                return 0f;
            const float minimumVisibleBlack = 0.0001f;
            const float zeroDetent = 0.01f;
            float logarithmicPosition = Mathf.InverseLerp(
                Mathf.Log10(minimumVisibleBlack),
                Mathf.Log10(GameDisplaySettings.MaximumBlackLevelNits),
                Mathf.Log10(
                    Mathf.Clamp(
                        nits,
                        minimumVisibleBlack,
                        GameDisplaySettings.MaximumBlackLevelNits
                    )
                )
            );
            return Mathf.Lerp(zeroDetent, 1f, logarithmicPosition);
        }
    }
}
