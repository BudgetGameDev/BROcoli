using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ResponsiveMainMenuLayout
{
    private void StyleButtons(Button[] buttons)
    {
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            bool primaryAction = button.name is "PlayButton" or "WavesButton" or "DungeonButton";
            Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Color.white;
            }

            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = primaryAction ? Primary : SurfaceVariant,
                highlightedColor = primaryAction ? PrimaryHover : Hex("#3A4B42"),
                pressedColor = primaryAction ? PrimaryPressed : Hex("#202C26"),
                selectedColor = primaryAction ? PrimaryHover : Hex("#3A4B42"),
                disabledColor = new Color(0.35f, 0.39f, 0.37f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };

            Shadow shadow = button.GetComponent<Shadow>();
            if (shadow == null)
                shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, primaryAction ? 0.42f : 0.28f);
            shadow.effectDistance = new Vector2(0f, primaryAction ? -4f : -2f);
            shadow.useGraphicAlpha = true;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                continue;

            if (materialFont != null)
                label.font = materialFont;
            label.color = OnSurface;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 2f;
            label.enableAutoSizing = false;
            label.margin = new Vector4(24f, 0f, 24f, 0f);
            Stretch(label.rectTransform);
        }
    }

    private void SetButtonLabel(string buttonName, string value)
    {
        Button button = FindButton(buttonName);
        TMP_Text label = button?.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = value;
    }

    private TMP_Text CreateText(
        string objectName,
        RectTransform parent,
        string value,
        float fontSize,
        Color color
    )
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (materialFont != null)
            text.font = materialFont;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.characterSpacing = 2f;
        return text;
    }

    private static RectTransform CreatePanel(string objectName, RectTransform parent, Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static RectTransform CreateRect(string objectName, RectTransform parent)
    {
        GameObject child = new(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void ReparentButtons(Button[] buttons, RectTransform parent)
    {
        foreach (Button button in buttons)
        {
            if (button != null)
                button.transform.SetParent(parent, false);
        }
    }

    private Button FindButton(string objectName)
    {
        Transform match = FindDescendant(transform, objectName);
        return match != null ? match.GetComponent<Button>() : null;
    }

    private static Transform FindDescendant(Transform parent, string objectName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == objectName)
                return child;

            Transform nested = FindDescendant(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private int GetVisibilitySignature()
    {
        int signature = modePanel != null && modePanel.gameObject.activeInHierarchy ? 1 : 0;
        for (int i = 0; i < mainButtons.Length; i++)
        {
            if (mainButtons[i] != null && mainButtons[i].gameObject.activeInHierarchy)
                signature |= 1 << (i + 1);
        }
        return signature;
    }

    private static void SetTopAnchored(RectTransform rect, float inset, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -inset);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetCenteredRect(RectTransform rect, float width, float height, float y)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static bool Approximately(Rect a, Rect b)
    {
        return Mathf.Abs(a.x - b.x) < 0.5f
            && Mathf.Abs(a.y - b.y) < 0.5f
            && Mathf.Abs(a.width - b.width) < 0.5f
            && Mathf.Abs(a.height - b.height) < 0.5f;
    }

    private static Color Hex(string value)
    {
        return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.white;
    }
}
