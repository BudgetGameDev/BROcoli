using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        float rowHeight = (top - bottom - titleHeight - buttonHeight - gap * 5f) / 3f;
        settingsTitle.fontSize = narrow ? 18f : (compact ? 19f : 22f);
        SetCenteredRect(settingsTitle.rectTransform, width, titleHeight, top - titleHeight * 0.5f);

        float y = top - titleHeight - gap - rowHeight * 0.5f;
        for (int i = 0; i < volumeRows.Length; i++)
        {
            SetCenteredRect(volumeRows[i], width, rowHeight, y);
            LayoutVolumeRow(i, width, rowHeight, compact, narrow);
            y -= rowHeight + gap;
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
        resetSettingsButton.transform.localPosition += Vector3.left * (actionWidth + gap) * 0.5f;
        backSettingsButton.transform.localPosition += Vector3.right * (actionWidth + gap) * 0.5f;
    }

    private void LayoutVolumeRow(int index, float width, float height, bool compact, bool narrow)
    {
        RectTransform row = volumeRows[index];
        TMP_Text[] labels = row.GetComponentsInChildren<TMP_Text>(true);
        float labelHeight = compact ? 17f : 22f;
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
        float trackHeight = compact ? 8f : 10f;
        SetCenteredRect(track, width - 14f, trackHeight, -height * 0.5f + trackHeight);
        RectTransform fillArea = track.GetChild(0) as RectTransform;
        RectTransform handleArea = track.GetChild(1) as RectTransform;
        Stretch(fillArea);
        Stretch(fillArea.GetChild(0) as RectTransform);
        Stretch(handleArea);
        RectTransform handle = handleArea.GetChild(0) as RectTransform;
        handle.sizeDelta = Vector2.one * (compact ? 18f : 22f);
    }
}
