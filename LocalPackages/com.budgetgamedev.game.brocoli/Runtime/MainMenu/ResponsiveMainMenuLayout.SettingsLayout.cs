using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private void LayoutSettingsPanel(
            float width,
            float top,
            float bottom,
            bool compact,
            bool narrow
        )
        {
            if (settingsPanel == null)
                return;

            Stretch(settingsPanel);
            float gap = compact ? 7f : 12f;
            float titleHeight = compact ? 24f : 32f;
            float buttonHeight = compact ? 42f : 54f;
            float labelHeight = compact ? 17f : 22f;
            float trackHeight = compact ? 8f : 10f;
            float handleSize = compact ? 18f : 22f;
            float labelToTrack = compact ? 6f : 9f;
            // A row is only as tall as its own label plus slider, so the label always reads
            // as belonging to the slider directly beneath it.
            float rowHeight = labelHeight + labelToTrack + handleSize;
            settingsTitle.fontSize = narrow ? 18f : (compact ? 19f : 22f);
            SetCenteredRect(
                settingsTitle.rectTransform,
                width,
                titleHeight,
                top - titleHeight * 0.5f
            );

            // Spare vertical space separates the rows instead of stretching them, so the
            // spacing between groups stays clearly larger than the spacing inside one.
            float regionTop = top - titleHeight - gap;
            float regionBottom = bottom + buttonHeight + gap;
            float rowGap = Mathf.Clamp(
                (regionTop - regionBottom - rowHeight * volumeRows.Length)
                    / (volumeRows.Length - 1),
                labelToTrack * 2f,
                compact ? 30f : 44f
            );
            float blockHeight = rowHeight * volumeRows.Length + rowGap * (volumeRows.Length - 1);
            float slack = Mathf.Max(0f, regionTop - regionBottom - blockHeight);
            float y = regionTop - slack * 0.5f - rowHeight * 0.5f;
            for (int i = 0; i < volumeRows.Length; i++)
            {
                SetCenteredRect(volumeRows[i], width, rowHeight, y);
                LayoutVolumeRow(
                    i,
                    width,
                    rowHeight,
                    labelHeight,
                    trackHeight,
                    handleSize,
                    narrow,
                    compact
                );
                y -= rowHeight + rowGap;
            }

            float actionWidth = (width - gap) * 0.5f;
            float actionY = bottom + buttonHeight * 0.5f;
            SetCenteredRect(
                resetSettingsButton.GetComponent<RectTransform>(),
                actionWidth,
                buttonHeight,
                actionY
            );
            SetCenteredRect(
                backSettingsButton.GetComponent<RectTransform>(),
                actionWidth,
                buttonHeight,
                actionY
            );
            resetSettingsButton.transform.localPosition +=
                Vector3.left * (actionWidth + gap) * 0.5f;
            backSettingsButton.transform.localPosition +=
                Vector3.right * (actionWidth + gap) * 0.5f;
        }

        private void LayoutVolumeRow(
            int index,
            float width,
            float height,
            float labelHeight,
            float trackHeight,
            float handleSize,
            bool narrow,
            bool compact
        )
        {
            RectTransform row = volumeRows[index];
            TMP_Text[] labels = row.GetComponentsInChildren<TMP_Text>(true);
            labels[0].fontSize = narrow ? 13f : (compact ? 14f : 16f);
            labels[1].fontSize = labels[0].fontSize;
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
            SetCenteredRect(track, width - 14f, trackHeight, -height * 0.5f + handleSize * 0.5f);
            RectTransform fillArea = track.GetChild(0) as RectTransform;
            RectTransform handleArea = track.GetChild(1) as RectTransform;
            Stretch(fillArea);
            Stretch(fillArea.GetChild(0) as RectTransform);
            Stretch(handleArea);
            RectTransform handle = handleArea.GetChild(0) as RectTransform;
            handle.sizeDelta = Vector2.one * handleSize;
        }
    }
}
