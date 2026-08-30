using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// The shared look of the game's menus: palette, selection accents and the
    /// small UI primitives every menu surface is built from. The main menu and the
    /// pause menu both draw from here, so restyling one never leaves the other
    /// behind.
    /// </summary>
    public static class MenuTheme
    {
        public static readonly Color Background = Hex("#0F1713");
        public static readonly Color HeroSurface = Hex("#173E2B");
        public static readonly Color CardSurface = Hex("#1D2923");
        public static readonly Color SurfaceVariant = Hex("#2A3831");
        public static readonly Color Primary = Hex("#43A047");
        public static readonly Color PrimaryHover = Hex("#55B95A");
        public static readonly Color PrimaryPressed = Hex("#347C38");
        public static readonly Color OnSurface = Hex("#F4F7F5");
        public static readonly Color OnSurfaceMuted = Hex("#B8C6BE");
        public static readonly Color Divider = new(0.65f, 0.84f, 0.71f, 0.22f);

        /// <summary>Outline drawn around the button the controller is on.</summary>
        public static readonly Color SelectionOutline = new(0.64f, 1f, 0.76f, 0.95f);
        public static readonly Vector2 SelectionThickness = new(5f, 5f);
        public const float SelectedScale = 1.06f;
        public const float SelectionLerpSpeed = 12f;

        public static RectTransform CreateRect(string objectName, RectTransform parent)
        {
            GameObject child = new(objectName, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform CreatePanel(
            string objectName,
            RectTransform parent,
            Color color
        )
        {
            RectTransform rect = CreateRect(objectName, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        public static TMP_Text CreateText(
            string objectName,
            RectTransform parent,
            string value,
            float fontSize,
            Color color,
            TMP_FontAsset font
        )
        {
            RectTransform rect = CreateRect(objectName, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(text, value, fontSize, color, font);
            return text;
        }

        /// <summary>Applies the menu text style to a label that already exists.</summary>
        public static void StyleText(
            TMP_Text text,
            string value,
            float fontSize,
            Color color,
            TMP_FontAsset font
        )
        {
            if (font != null)
                text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.characterSpacing = 2f;
            text.enableAutoSizing = false;
        }

        /// <summary>
        /// Paints a button in the menu style. Primary actions carry the green fill,
        /// everything else sits on the surface variant.
        /// </summary>
        public static void StyleButton(Button button, bool primaryAction, TMP_FontAsset font)
        {
            if (button == null)
                return;

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
                return;

            if (font != null)
                label.font = font;
            label.color = OnSurface;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 2f;
            label.enableAutoSizing = false;
            label.margin = new Vector4(24f, 0f, 24f, 0f);
            Stretch(label.rectTransform);
        }

        /// <summary>Adds the soft drop shadow the menu cards sit on.</summary>
        public static void AddCardShadow(RectTransform card)
        {
            Shadow shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.48f);
            shadow.effectDistance = new Vector2(0f, -12f);
            shadow.useGraphicAlpha = true;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void SetTopAnchored(
            RectTransform rect,
            float inset,
            float width,
            float height
        )
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -inset);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void SetCenteredRect(RectTransform rect, float width, float height, float y)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Pins a rect to the device safe area, in canvas space.</summary>
        public static void ApplySafeArea(RectTransform safeArea, Rect pixelSafeArea)
        {
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            safeArea.anchorMin = new Vector2(
                pixelSafeArea.xMin / width,
                pixelSafeArea.yMin / height
            );
            safeArea.anchorMax = new Vector2(
                pixelSafeArea.xMax / width,
                pixelSafeArea.yMax / height
            );
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
        }

        public static bool Approximately(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < 0.5f
                && Mathf.Abs(a.y - b.y) < 0.5f
                && Mathf.Abs(a.width - b.width) < 0.5f
                && Mathf.Abs(a.height - b.height) < 0.5f;
        }

        public static Transform FindDescendant(Transform parent, string objectName)
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

        public static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.white;
        }
    }
}
