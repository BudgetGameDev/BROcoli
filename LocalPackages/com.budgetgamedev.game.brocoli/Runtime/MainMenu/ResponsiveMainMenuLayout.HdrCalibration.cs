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
            PaperWhite,
            BlackLevel,
        }

        private const int HdrCalibrationStepCount = 3;

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
        private float initialPeakBrightness;
        private float initialPaperWhite;
        private float initialBlackLevel;

        public static bool HdrCalibrationOpen { get; private set; }

        private void OpenHdrCalibration()
        {
            ProceduralUIAudio.PlaySelect();
            initialHdrEnabled = GameDisplaySettings.HdrEnabled;
            initialPeakBrightness = GameDisplaySettings.PeakBrightnessNits;
            initialPaperWhite = GameDisplaySettings.PaperWhiteNits;
            initialBlackLevel = GameDisplaySettings.BlackLevelNits;
            GameDisplaySettings.SetHdrEnabled(true);

            HdrCalibrationOpen = true;
            settingsPanel.gameObject.SetActive(false);
            hdrCalibrationPanel.gameObject.SetActive(true);
            SetHdrCalibrationStep(HdrCalibrationStep.PeakBrightness);
            SelectHdrCalibrationControl(0, false);
            ApplyResponsiveLayout(true);
        }

        private void CloseHdrCalibration(bool save)
        {
            if (!HdrCalibrationOpen)
                return;

            if (!save)
            {
                GameDisplaySettings.SetCalibration(
                    initialPeakBrightness,
                    initialPaperWhite,
                    initialBlackLevel
                );
                GameDisplaySettings.SetHdrEnabled(initialHdrEnabled);
            }

            HdrCalibrationOpen = false;
            hdrCalibrationPanel.gameObject.SetActive(false);
            if (SettingsOpen)
            {
                settingsPanel.gameObject.SetActive(true);
                SyncHdrControl();
                int index = System.Array.IndexOf(settingsSelectables, hdrCalibrationButton);
                SelectSetting(Mathf.Max(0, index), false);
            }
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
                    hdrCalibrationStepLabel.text = "STEP 1 OF 3  •  PEAK BRIGHTNESS";
                    hdrCalibrationInstructions.text =
                        "Increase the value until the center square is only just distinguishable "
                        + "from the surrounding bright patch.";
                    hdrCalibrationSlider.minValue = GameDisplaySettings.MinimumPeakBrightnessNits;
                    hdrCalibrationSlider.maxValue = GameDisplaySettings.MaximumPeakBrightnessNits;
                    hdrCalibrationSlider.wholeNumbers = true;
                    hdrCalibrationSlider.SetValueWithoutNotify(
                        GameDisplaySettings.PeakBrightnessNits
                    );
                    break;
                case HdrCalibrationStep.PaperWhite:
                    hdrCalibrationStepLabel.text = "STEP 2 OF 3  •  PAPER WHITE";
                    hdrCalibrationInstructions.text =
                        "Adjust until the white patch is comfortably bright for menus and text, "
                        + "without looking dull or glaring.";
                    hdrCalibrationSlider.minValue = GameDisplaySettings.MinimumPaperWhiteNits;
                    hdrCalibrationSlider.maxValue = Mathf.Min(
                        GameDisplaySettings.MaximumPaperWhiteNits,
                        GameDisplaySettings.PeakBrightnessNits
                    );
                    hdrCalibrationSlider.wholeNumbers = true;
                    hdrCalibrationSlider.SetValueWithoutNotify(GameDisplaySettings.PaperWhiteNits);
                    break;
                default:
                    hdrCalibrationStepLabel.text = "STEP 3 OF 3  •  BLACK LEVEL";
                    hdrCalibrationInstructions.text =
                        "Raise the value from zero until the center square is barely visible, "
                        + "then stop before the background looks gray.";
                    hdrCalibrationSlider.minValue = 0f;
                    hdrCalibrationSlider.maxValue = 1f;
                    hdrCalibrationSlider.wholeNumbers = false;
                    hdrCalibrationSlider.SetValueWithoutNotify(
                        BlackLevelToSlider(GameDisplaySettings.BlackLevelNits)
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
                    GameDisplaySettings.SetPeakBrightness(Mathf.Round(value / 25f) * 25f);
                    break;
                case HdrCalibrationStep.PaperWhite:
                    GameDisplaySettings.SetPaperWhite(Mathf.Round(value / 5f) * 5f);
                    break;
                default:
                    GameDisplaySettings.SetBlackLevel(SliderToBlackLevel(value));
                    break;
            }
            suppressHdrCalibrationCallback = true;
            hdrCalibrationSlider.SetValueWithoutNotify(
                hdrCalibrationStep switch
                {
                    HdrCalibrationStep.PeakBrightness => GameDisplaySettings.PeakBrightnessNits,
                    HdrCalibrationStep.PaperWhite => GameDisplaySettings.PaperWhiteNits,
                    _ => BlackLevelToSlider(GameDisplaySettings.BlackLevelNits),
                }
            );
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
                    hdrCalibrationValue.text =
                        $"{Mathf.RoundToInt(GameDisplaySettings.PeakBrightnessNits)} NITS";
                    backgroundNits = 0f;
                    referenceNits = GameDisplaySettings.PeakBrightnessNits * 0.9f;
                    markNits = GameDisplaySettings.PeakBrightnessNits;
                    break;
                case HdrCalibrationStep.PaperWhite:
                    hdrCalibrationValue.text =
                        $"{Mathf.RoundToInt(GameDisplaySettings.PaperWhiteNits)} NITS";
                    backgroundNits = GameDisplaySettings.PaperWhiteNits * 0.08f;
                    referenceNits = GameDisplaySettings.PaperWhiteNits;
                    markNits = GameDisplaySettings.PaperWhiteNits * 0.8f;
                    break;
                default:
                    hdrCalibrationValue.text = $"{GameDisplaySettings.BlackLevelNits:0.0000} NITS";
                    backgroundNits = 0f;
                    referenceNits = 0f;
                    markNits = Mathf.Max(0.0001f, GameDisplaySettings.BlackLevelNits);
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
