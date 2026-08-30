using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Hub
{
    public sealed partial class GameLauncher
    {
        private static RectTransform CreateRect(string name, Transform parent)
        {
            var host = new GameObject(name, typeof(RectTransform));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>
        /// Uses the built-in legacy font so the launcher renders with no font asset
        /// of its own. A brand-neutral shell should not carry any game's typeface.
        /// </summary>
        private static Text AddText(
            RectTransform rect,
            string value,
            int size,
            Color color,
            TextAnchor alignment
        )
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
