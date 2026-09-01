using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsivePauseMenuLayout
    {
        private enum PauseHdrCalibrationStep
        {
            PeakBrightness,
            BlackLevel,
        }

        private RectTransform hdrCalibrationPanel;
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
        private Selectable[] hdrCalibrationSelectables;
        private Material[] hdrPreviewMaterials;
        private PauseHdrCalibrationStep hdrCalibrationStep;
        private int selectedHdrCalibrationControl;
        private float lastHdrCalibrationNavTime;
        private bool suppressHdrCalibrationCallback;
        private bool initialHdrEnabled;
        private float pendingPeakBrightness;
        private float pendingBlackLevel;

        public bool HdrCalibrationOpen { get; private set; }

        private void OpenHdrCalibration()
        {
            ProceduralUIAudio.PlaySelect();
            initialHdrEnabled = GameDisplaySettings.HdrEnabled;
            pendingPeakBrightness = GameDisplaySettings.PeakBrightnessNits;
            pendingBlackLevel = GameDisplaySettings.BlackLevelNits;
            GameDisplaySettings.SetHdrEnabled(true);
            GameDisplaySettings.BeginHdrCalibrationPreview();
            HdrCalibrationOpen = true;
            HdrDetailsOpen = false;
            hdrDetailsPanel.gameObject.SetActive(false);
            hdrCalibrationPanel.gameObject.SetActive(true);
            title.text = "HDR CALIBRATION";
            footer.text = "ESC  ·  B  TO CANCEL";
            SetPauseHdrCalibrationStep(PauseHdrCalibrationStep.PeakBrightness);
            SelectPauseHdrCalibrationControl(0, false);
            ApplyResponsiveLayout(true);
        }

        private void EndHdrCalibration(bool save)
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
            HdrCalibrationOpen = false;
            hdrCalibrationPanel.gameObject.SetActive(false);
            if (SettingsOpen)
            {
                HdrDetailsOpen = true;
                hdrDetailsPanel.gameObject.SetActive(true);
                title.text = "HDR OUTPUT";
                footer.text = "ESC  ·  B  TO SETTINGS";
                SyncPauseHdrDetails();
                SelectHdrDetail(
                    System.Array.IndexOf(hdrDetailSelectables, hdrCalibrateButton),
                    false
                );
            }
            ApplyResponsiveLayout(true);
        }

        private void ResetPauseHdrCalibrationToSystem()
        {
            if (!HdrCalibrationOpen)
                return;
            ProceduralUIAudio.PlaySelect();
            GameDisplaySettings.EndHdrCalibrationPreview();
            GameDisplaySettings.ResetToSystemCalibration();
            HdrCalibrationOpen = false;
            hdrCalibrationPanel.gameObject.SetActive(false);
            HdrDetailsOpen = true;
            hdrDetailsPanel.gameObject.SetActive(true);
            title.text = "HDR OUTPUT";
            footer.text = "ESC  ·  B  TO SETTINGS";
            SyncPauseHdrDetails();
            SelectHdrDetail(System.Array.IndexOf(hdrDetailSelectables, hdrCalibrateButton), false);
            ApplyResponsiveLayout(true);
        }

        private void PreviousPauseHdrCalibrationStep()
        {
            ProceduralUIAudio.PlaySelect();
            if (hdrCalibrationStep == PauseHdrCalibrationStep.PeakBrightness)
                EndHdrCalibration(false);
            else
                SetPauseHdrCalibrationStep(PauseHdrCalibrationStep.PeakBrightness);
        }

        private void NextPauseHdrCalibrationStep()
        {
            ProceduralUIAudio.PlaySelect();
            if (hdrCalibrationStep == PauseHdrCalibrationStep.BlackLevel)
                EndHdrCalibration(true);
            else
                SetPauseHdrCalibrationStep(PauseHdrCalibrationStep.BlackLevel);
        }

        private void SetPauseHdrCalibrationStep(PauseHdrCalibrationStep step)
        {
            hdrCalibrationStep = step;
            suppressHdrCalibrationCallback = true;
            if (step == PauseHdrCalibrationStep.PeakBrightness)
            {
                hdrCalibrationStepLabel.text = "STEP 1 OF 2  •  MAXIMUM LUMINANCE";
                hdrCalibrationInstructions.text =
                    "Sets the brightest highlight your display can reproduce. Move right until "
                    + "the center square disappears, then move left one step so it is barely visible.";
                hdrCalibrationSlider.minValue = GameDisplaySettings.MinimumPeakBrightnessNits;
                hdrCalibrationSlider.maxValue = GameDisplaySettings.MaximumPeakBrightnessNits;
                hdrCalibrationSlider.wholeNumbers = true;
                hdrCalibrationSlider.SetValueWithoutNotify(pendingPeakBrightness);
            }
            else
            {
                hdrCalibrationStepLabel.text = "STEP 2 OF 2  •  MINIMUM LUMINANCE";
                hdrCalibrationInstructions.text =
                    "Sets the darkest shadow your display can distinguish. Start at zero and move "
                    + "right until the center square is barely visible while the surrounding area stays black.";
                hdrCalibrationSlider.minValue = 0f;
                hdrCalibrationSlider.maxValue = 1f;
                hdrCalibrationSlider.wholeNumbers = false;
                hdrCalibrationSlider.SetValueWithoutNotify(
                    ResponsiveMainMenuLayout.BlackLevelToSlider(pendingBlackLevel)
                );
            }
            suppressHdrCalibrationCallback = false;
            hdrCalibrationBackButton.GetComponentInChildren<TMP_Text>(true).text =
                step == PauseHdrCalibrationStep.PeakBrightness ? "CANCEL" : "BACK";
            hdrCalibrationNextButton.GetComponentInChildren<TMP_Text>(true).text =
                step == PauseHdrCalibrationStep.BlackLevel ? "APPLY" : "NEXT";
            SyncPauseHdrCalibrationPreview();
        }

        private void OnPauseHdrCalibrationSliderChanged(float value)
        {
            if (suppressHdrCalibrationCallback)
                return;
            if (hdrCalibrationStep == PauseHdrCalibrationStep.PeakBrightness)
            {
                pendingPeakBrightness = Mathf.Clamp(
                    Mathf.Round(value / 25f) * 25f,
                    GameDisplaySettings.MinimumPeakBrightnessNits,
                    GameDisplaySettings.MaximumPeakBrightnessNits
                );
                hdrCalibrationSlider.SetValueWithoutNotify(pendingPeakBrightness);
            }
            else
            {
                pendingBlackLevel = ResponsiveMainMenuLayout.SliderToBlackLevel(value);
                hdrCalibrationSlider.SetValueWithoutNotify(
                    ResponsiveMainMenuLayout.BlackLevelToSlider(pendingBlackLevel)
                );
            }
            SyncPauseHdrCalibrationPreview();
        }

        private void SyncPauseHdrCalibrationPreview()
        {
            float referenceNits;
            float markNits;
            if (hdrCalibrationStep == PauseHdrCalibrationStep.PeakBrightness)
            {
                hdrCalibrationValue.text = $"{Mathf.RoundToInt(pendingPeakBrightness)} NITS";
                referenceNits = pendingPeakBrightness * 0.9f;
                markNits = pendingPeakBrightness;
            }
            else
            {
                hdrCalibrationValue.text = $"{pendingBlackLevel:0.0000} NITS";
                referenceNits = 0f;
                markNits = pendingBlackLevel;
            }
            SetPauseHdrPreviewLuminance(hdrPreviewBackground, 0f);
            SetPauseHdrPreviewLuminance(hdrPreviewReference, referenceNits);
            SetPauseHdrPreviewLuminance(hdrPreviewMark, markNits);
        }

        private static void SetPauseHdrPreviewLuminance(Image image, float nits)
        {
            if (image?.material == null)
                return;
            // The HDR grade tone maps this patch like any other scene colour, so the value
            // that shows the wanted luminance has to come back through the tone map's inverse.
            float sceneValue = GameDisplaySettings.HdrSceneValueForNits(nits);
            image.material.SetColor("_Color", new Color(sceneValue, sceneValue, sceneValue, 1f));
        }
    }
}
