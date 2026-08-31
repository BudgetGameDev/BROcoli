using TMPro;
using UnityEngine;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private void LayoutHdrCalibrationPanel(
            float width,
            float top,
            float bottom,
            bool compact,
            bool narrow
        )
        {
            if (hdrCalibrationPanel == null)
                return;

            Stretch(hdrCalibrationPanel);
            float gap = compact ? 7f : 11f;
            float titleHeight = compact ? 24f : 32f;
            float stepHeight = compact ? 19f : 24f;
            float instructionsHeight = compact ? 42f : 54f;
            float valueHeight = compact ? 24f : 30f;
            float sliderHeight = compact ? 8f : 10f;
            float handleSize = compact ? 16f : 22f;
            float buttonHeight = compact ? 40f : 52f;

            float cursor = top;
            SetCenteredRect(
                hdrCalibrationTitle.rectTransform,
                width,
                titleHeight,
                cursor - titleHeight * 0.5f
            );
            hdrCalibrationTitle.fontSize = narrow ? 18f : (compact ? 19f : 22f);
            cursor -= titleHeight + gap;

            SetCenteredRect(
                hdrCalibrationStepLabel.rectTransform,
                width,
                stepHeight,
                cursor - stepHeight * 0.5f
            );
            hdrCalibrationStepLabel.fontSize = narrow ? 11f : (compact ? 12f : 14f);
            cursor -= stepHeight + gap;

            SetCenteredRect(
                hdrCalibrationInstructions.rectTransform,
                width,
                instructionsHeight,
                cursor - instructionsHeight * 0.5f
            );
            hdrCalibrationInstructions.fontSize = narrow ? 11f : (compact ? 12f : 14f);
            cursor -= instructionsHeight + gap;

            float reservedBelow = valueHeight + sliderHeight + handleSize + buttonHeight + gap * 4f;
            float previewHeight = Mathf.Clamp(
                cursor - bottom - reservedBelow,
                compact ? 100f : 130f,
                compact ? 175f : 230f
            );
            SetCenteredRect(
                hdrCalibrationPreview,
                Mathf.Min(width, previewHeight * (narrow ? 1.5f : 1.8f)),
                previewHeight,
                cursor - previewHeight * 0.5f
            );
            LayoutHdrCalibrationPreview(previewHeight, narrow);
            cursor -= previewHeight + gap;

            SetCenteredRect(
                hdrCalibrationValue.rectTransform,
                width,
                valueHeight,
                cursor - valueHeight * 0.5f
            );
            hdrCalibrationValue.fontSize = narrow ? 16f : (compact ? 18f : 20f);
            cursor -= valueHeight + gap;

            RectTransform track = hdrCalibrationSlider.GetComponent<RectTransform>();
            SetCenteredRect(track, width - 18f, sliderHeight, cursor - handleSize * 0.5f);
            RectTransform fillArea = track.GetChild(0) as RectTransform;
            RectTransform handleArea = track.GetChild(1) as RectTransform;
            Stretch(fillArea);
            Stretch(fillArea.GetChild(0) as RectTransform);
            Stretch(handleArea);
            (handleArea.GetChild(0) as RectTransform).sizeDelta = Vector2.one * handleSize;

            float buttonWidth = (width - gap) * 0.5f;
            float buttonY = bottom + buttonHeight * 0.5f;
            SetCenteredRect(
                hdrCalibrationBackButton.GetComponent<RectTransform>(),
                buttonWidth,
                buttonHeight,
                buttonY
            );
            SetCenteredRect(
                hdrCalibrationNextButton.GetComponent<RectTransform>(),
                buttonWidth,
                buttonHeight,
                buttonY
            );
            hdrCalibrationBackButton.transform.localPosition +=
                Vector3.left * (buttonWidth + gap) * 0.5f;
            hdrCalibrationNextButton.transform.localPosition +=
                Vector3.right * (buttonWidth + gap) * 0.5f;
            foreach (
                ButtonLabelSize size in new[]
                {
                    new ButtonLabelSize(hdrCalibrationBackButton, narrow ? 14f : 17f),
                    new ButtonLabelSize(hdrCalibrationNextButton, narrow ? 14f : 17f),
                }
            )
            {
                TMP_Text label = size.Button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.fontSize = size.Size;
            }
        }

        private void LayoutHdrCalibrationPreview(float height, bool narrow)
        {
            Stretch(hdrPreviewBackground.rectTransform);
            float referenceWidth = hdrCalibrationPreview.rect.width * (narrow ? 0.66f : 0.58f);
            float referenceHeight = height * 0.65f;
            SetCenteredRect(hdrPreviewReference.rectTransform, referenceWidth, referenceHeight, 0f);
            float markSize = Mathf.Min(referenceWidth, referenceHeight) * 0.2f;
            SetCenteredRect(hdrPreviewMark.rectTransform, markSize, markSize, 0f);
        }

        private readonly struct ButtonLabelSize
        {
            internal ButtonLabelSize(UnityEngine.UI.Button button, float size)
            {
                Button = button;
                Size = size;
            }

            internal UnityEngine.UI.Button Button { get; }
            internal float Size { get; }
        }
    }
}
