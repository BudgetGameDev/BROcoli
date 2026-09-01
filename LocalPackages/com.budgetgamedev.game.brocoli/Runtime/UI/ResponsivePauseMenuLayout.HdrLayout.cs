using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsivePauseMenuLayout
    {
        private void LayoutPauseHdrDetails(
            float width,
            float top,
            float bottom,
            bool compact,
            bool narrow
        )
        {
            if (hdrDetailsPanel == null)
                return;
            Stretch(hdrDetailsPanel);
            float gap = compact ? 6f : 10f;
            float explanationHeight = compact ? 43f : 56f;
            float buttonHeight = compact ? 40f : 50f;
            SetCenteredRect(
                hdrDetailsExplanation.rectTransform,
                width,
                explanationHeight,
                top - explanationHeight * 0.5f
            );
            hdrDetailsExplanation.fontSize = narrow ? 10f : (compact ? 11f : 13f);
            float valuesTop = top - explanationHeight - gap;
            float buttonsHeight = buttonHeight * 2f + gap;
            float valuesBottom = bottom + buttonsHeight + gap * 2f;
            float valuesHeight = Mathf.Max(140f, valuesTop - valuesBottom);
            SetCenteredRect(
                hdrDetailsValues.rectTransform,
                width,
                valuesHeight,
                valuesTop - valuesHeight * 0.5f
            );
            hdrDetailsValues.fontSize = narrow ? 9f : (compact ? 10f : 12f);
            hdrDetailsValues.lineSpacing = compact ? -7f : -2f;

            float buttonWidth = (width - gap) * 0.5f;
            for (int index = 0; index < hdrDetailButtons.Length; index++)
            {
                int row = index / 2;
                bool right = index % 2 != 0;
                float y = bottom + buttonHeight * 0.5f + (1 - row) * (buttonHeight + gap);
                float x = (buttonWidth + gap) * (right ? 0.5f : -0.5f);
                RectTransform rect = hdrDetailButtons[index].GetComponent<RectTransform>();
                SetCenteredRect(rect, buttonWidth, buttonHeight, y);
                rect.anchoredPosition = new Vector2(x, y);
                TMP_Text label = hdrDetailButtons[index].GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.fontSize = narrow ? 10f : (compact ? 12f : 14f);
            }
        }

        private void LayoutPauseHdrCalibration(
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
            float gap = compact ? 6f : 10f;
            float stepHeight = compact ? 20f : 25f;
            float instructionsHeight = compact ? 44f : 58f;
            float valueHeight = compact ? 24f : 30f;
            float handleSize = compact ? 16f : 22f;
            float buttonHeight = compact ? 39f : 50f;
            float cursor = top;

            SetCenteredRect(
                hdrCalibrationStepLabel.rectTransform,
                width,
                stepHeight,
                cursor - stepHeight * 0.5f
            );
            hdrCalibrationStepLabel.fontSize = narrow ? 10f : (compact ? 12f : 14f);
            cursor -= stepHeight + gap;
            SetCenteredRect(
                hdrCalibrationInstructions.rectTransform,
                width,
                instructionsHeight,
                cursor - instructionsHeight * 0.5f
            );
            hdrCalibrationInstructions.fontSize = narrow ? 10f : (compact ? 11f : 13f);
            cursor -= instructionsHeight + gap;

            float reserved = valueHeight + handleSize + buttonHeight * 2f + gap * 6f;
            float previewHeight = Mathf.Clamp(
                cursor - bottom - reserved,
                compact ? 90f : 120f,
                compact ? 170f : 225f
            );
            SetCenteredRect(
                hdrCalibrationPreview,
                Mathf.Min(width, previewHeight * (narrow ? 1.5f : 1.8f)),
                previewHeight,
                cursor - previewHeight * 0.5f
            );
            float referenceWidth = hdrCalibrationPreview.rect.width * (narrow ? 0.66f : 0.58f);
            float referenceHeight = previewHeight * 0.65f;
            SetCenteredRect(hdrPreviewReference.rectTransform, referenceWidth, referenceHeight, 0f);
            float markSize = Mathf.Min(referenceWidth, referenceHeight) * 0.2f;
            SetCenteredRect(hdrPreviewMark.rectTransform, markSize, markSize, 0f);
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
            SetCenteredRect(track, width - 18f, compact ? 8f : 10f, cursor - handleSize * 0.5f);
            RectTransform fillArea = track.GetChild(0) as RectTransform;
            RectTransform handleArea = track.GetChild(1) as RectTransform;
            Stretch(fillArea);
            Stretch(fillArea.GetChild(0) as RectTransform);
            Stretch(handleArea);
            (handleArea.GetChild(0) as RectTransform).sizeDelta = Vector2.one * handleSize;

            float systemY = bottom + buttonHeight + gap + buttonHeight * 0.5f;
            SetCenteredRect(
                hdrCalibrationSystemButton.GetComponent<RectTransform>(),
                width,
                buttonHeight,
                systemY
            );
            float buttonWidth = (width - gap) * 0.5f;
            PositionSplitButton(
                hdrCalibrationBackButton,
                buttonWidth,
                buttonHeight,
                bottom + buttonHeight * 0.5f,
                -1f,
                gap
            );
            PositionSplitButton(
                hdrCalibrationNextButton,
                buttonWidth,
                buttonHeight,
                bottom + buttonHeight * 0.5f,
                1f,
                gap
            );
            foreach (
                Button button in new[]
                {
                    hdrCalibrationSystemButton,
                    hdrCalibrationBackButton,
                    hdrCalibrationNextButton,
                }
            )
            {
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.fontSize = narrow ? 11f : (compact ? 13f : 15f);
            }
        }
    }
}
