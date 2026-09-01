using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private void LayoutHdrDetailsPanel(
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
            float titleHeight = compact ? 23f : 31f;
            float subtitleHeight = compact ? 17f : 22f;
            float explanationHeight = compact ? 42f : 54f;
            float buttonHeight = compact ? 40f : 50f;
            float cursor = top;

            SetCenteredRect(
                hdrDetailsTitle.rectTransform,
                width,
                titleHeight,
                cursor - titleHeight * 0.5f
            );
            hdrDetailsTitle.fontSize = narrow ? 18f : (compact ? 19f : 22f);
            cursor -= titleHeight + gap;
            SetCenteredRect(
                hdrDetailsSubtitle.rectTransform,
                width,
                subtitleHeight,
                cursor - subtitleHeight * 0.5f
            );
            hdrDetailsSubtitle.fontSize = narrow ? 10f : (compact ? 11f : 13f);
            cursor -= subtitleHeight + gap;
            SetCenteredRect(
                hdrDetailsExplanation.rectTransform,
                width,
                explanationHeight,
                cursor - explanationHeight * 0.5f
            );
            hdrDetailsExplanation.fontSize = narrow ? 10f : (compact ? 11f : 13f);
            cursor -= explanationHeight + gap;

            float buttonsHeight = buttonHeight * 2f + gap;
            float valuesBottom = bottom + buttonsHeight + gap * 2f;
            float valuesHeight = Mathf.Max(150f, cursor - valuesBottom);
            SetCenteredRect(
                hdrDetailsValues.rectTransform,
                width,
                valuesHeight,
                cursor - valuesHeight * 0.5f
            );
            hdrDetailsValues.fontSize = narrow ? 10f : (compact ? 11f : 13f);
            hdrDetailsValues.lineSpacing = compact ? -7f : -2f;

            float buttonWidth = (width - gap) * 0.5f;
            for (int index = 0; index < hdrDetailsActionButtons.Length; index++)
            {
                int row = index / 2;
                bool right = index % 2 != 0;
                float y = bottom + buttonHeight * 0.5f + (1 - row) * (buttonHeight + gap);
                float x = (buttonWidth + gap) * (right ? 0.5f : -0.5f);
                RectTransform rect = hdrDetailsActionButtons[index].GetComponent<RectTransform>();
                SetCenteredRect(rect, buttonWidth, buttonHeight, y);
                rect.anchoredPosition = new Vector2(x, y);
                TMP_Text label = hdrDetailsActionButtons[index]
                    .GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.fontSize = narrow ? 11f : (compact ? 13f : 15f);
            }
        }
    }
}
