using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsivePauseMenuLayout
    {
        private void LayoutSettingsPanels(
            float width,
            float top,
            float bottom,
            bool compact,
            bool narrow
        )
        {
            if (settingsPanel == null)
                return;
            nvidiaPage?.Layout(width, top, bottom, compact, narrow);
            LayoutGeneralSettings(width, top, bottom, compact, narrow);
            LayoutPauseHdrDetails(width, top, bottom, compact, narrow);
            LayoutPauseHdrCalibration(width, top, bottom, compact, narrow);
        }

        private void LayoutGeneralSettings(
            float width,
            float top,
            float bottom,
            bool compact,
            bool narrow
        )
        {
            Stretch(settingsPanel);
            float gap = compact ? 7f : 12f;
            float buttonHeight = compact ? 42f : 54f;
            float labelHeight = compact ? 14f : 22f;
            float handleSize = compact ? 15f : 22f;
            float rowHeight = labelHeight + (compact ? 4f : 9f) + handleSize;
            float regionBottom = bottom + buttonHeight + gap;
            float rowGap = Mathf.Clamp(
                (top - regionBottom - rowHeight * settingsRows.Length) / (settingsRows.Length - 1),
                compact ? 14f : 20f,
                compact ? 30f : 44f
            );
            float blockHeight = rowHeight * settingsRows.Length + rowGap * 3f;
            float y =
                top - Mathf.Max(0f, top - regionBottom - blockHeight) * 0.5f - rowHeight * 0.5f;
            for (int index = 0; index < settingsRows.Length; index++)
            {
                SetCenteredRect(settingsRows[index], width, rowHeight, y);
                if (index < 3)
                    LayoutPauseVolumeRow(
                        index,
                        width,
                        rowHeight,
                        labelHeight,
                        handleSize,
                        narrow,
                        compact
                    );
                else
                    LayoutPauseHdrRow(width, rowHeight, labelHeight, narrow, compact);
                y -= rowHeight + rowGap;
            }

            float actionWidth = (width - gap * 2) / 3;
            float actionY = bottom + buttonHeight * 0.5f;
            Button[] actions = { nvidiaSettingsButton, resetSettingsButton, backSettingsButton };
            for (int i = 0; i < actions.Length; i++)
            {
                PositionSplitButton(
                    actions[i],
                    actionWidth,
                    buttonHeight,
                    actionY,
                    (i - 1) * 2,
                    gap
                );
                var label = actions[i].GetComponentInChildren<TMP_Text>();
                label.margin = new Vector4(4, 0, 4, 0);
                label.enableAutoSizing = true;
                label.fontSizeMin = 9;
                label.fontSizeMax = compact ? 12 : 16;
            }
        }

        private void LayoutPauseVolumeRow(
            int index,
            float width,
            float height,
            float labelHeight,
            float handleSize,
            bool narrow,
            bool compact
        )
        {
            TMP_Text[] labels = settingsRows[index].GetComponentsInChildren<TMP_Text>(true);
            float fontSize = narrow ? 13f : (compact ? 14f : 16f);
            labels[0].fontSize = fontSize;
            labels[1].fontSize = fontSize;
            labels[0].rectTransform.anchorMin = new Vector2(0f, 1f);
            labels[0].rectTransform.anchorMax = new Vector2(0.75f, 1f);
            labels[0].rectTransform.pivot = new Vector2(0f, 1f);
            labels[0].rectTransform.offsetMin = new Vector2(0f, -labelHeight);
            labels[0].rectTransform.offsetMax = Vector2.zero;
            labels[1].rectTransform.anchorMin = new Vector2(0.75f, 1f);
            labels[1].rectTransform.anchorMax = Vector2.one;
            labels[1].rectTransform.pivot = Vector2.one;
            labels[1].rectTransform.offsetMin = new Vector2(0f, -labelHeight);
            labels[1].rectTransform.offsetMax = Vector2.zero;

            RectTransform track = volumeSliders[index].GetComponent<RectTransform>();
            SetCenteredRect(
                track,
                width - 14f,
                compact ? 8f : 10f,
                -height * 0.5f + handleSize * 0.5f
            );
            RectTransform fillArea = track.GetChild(0) as RectTransform;
            RectTransform handleArea = track.GetChild(1) as RectTransform;
            Stretch(fillArea);
            Stretch(fillArea.GetChild(0) as RectTransform);
            Stretch(handleArea);
            (handleArea.GetChild(0) as RectTransform).sizeDelta = Vector2.one * handleSize;
        }

        private void LayoutPauseHdrRow(
            float width,
            float height,
            float labelHeight,
            bool narrow,
            bool compact
        )
        {
            TMP_Text label = settingsRows[3].Find("HdrOutputLabel")?.GetComponent<TMP_Text>();
            label.fontSize = narrow ? 13f : (compact ? 14f : 16f);
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(0.4f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.offsetMin = new Vector2(0f, -labelHeight);
            label.rectTransform.offsetMax = Vector2.zero;
            float buttonGap = compact ? 6f : 9f;
            float available = width * (narrow ? 0.62f : 0.58f);
            float buttonHeight = compact ? 25f : 31f;
            float toggleWidth = available * 0.28f;
            float detailsWidth = available - toggleWidth - buttonGap;
            PositionTopRightButton(hdrDetailsButton, detailsWidth, buttonHeight, 0f);
            PositionTopRightButton(
                hdrToggleButton,
                toggleWidth,
                buttonHeight,
                -detailsWidth - buttonGap
            );
            hdrToggleValue.fontSize = narrow ? 12f : 14f;
            hdrDetailsButton.GetComponentInChildren<TMP_Text>(true).fontSize = narrow ? 11f : 13f;
            hdrStatus.fontSize = narrow ? 9f : 10f;
            hdrStatus.rectTransform.anchorMin = Vector2.zero;
            hdrStatus.rectTransform.anchorMax = new Vector2(1f, 0f);
            hdrStatus.rectTransform.pivot = Vector2.zero;
            hdrStatus.rectTransform.sizeDelta = new Vector2(
                0f,
                Mathf.Max(12f, height - buttonHeight)
            );
            hdrStatus.rectTransform.anchoredPosition = Vector2.zero;
        }

        private static void PositionTopRightButton(
            Button button,
            float width,
            float height,
            float x
        )
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        private static void PositionSplitButton(
            Button button,
            float width,
            float height,
            float y,
            float direction,
            float gap
        )
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            SetCenteredRect(rect, width, height, y);
            rect.anchoredPosition = new Vector2(direction * (width + gap) * 0.5f, y);
        }
    }
}
