using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Builds the "rotate your phone" overlay shown while the screen is portrait.
    /// Split out of <see cref="ForceLandscapeAspect"/> so the state machine that
    /// decides when to show it stays readable next to the widget construction.
    /// </summary>
    public static partial class ForceLandscapeAspect
    {
        internal static void CreateRotateOverlay()
        {
            // Create canvas
            _rotateOverlay = new GameObject("[RotatePhoneOverlay]");
            KeepAcrossScenes(_rotateOverlay);

            Canvas canvas = _rotateOverlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // On top of everything

            CanvasScaler scaler = _rotateOverlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // Portrait reference
            scaler.matchWidthOrHeight = 0.5f;

            _rotateOverlay.AddComponent<GraphicRaycaster>();

            // Dark background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(_rotateOverlay.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.9f);

            // Container for content
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(_rotateOverlay.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(400, 300);

            VerticalLayoutGroup layout = contentObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 30;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Phone icon with rotation arrow (using UI elements)
            GameObject iconObj = new GameObject("PhoneIcon");
            iconObj.transform.SetParent(contentObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(120, 120);

            // Create a simple phone shape
            CreatePhoneIcon(iconObj);

            // Add rotation animation
            iconObj.AddComponent<RotateAnimator>();

            // Text message
            GameObject textObj = new GameObject("Message");
            textObj.transform.SetParent(contentObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(350, 100);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "Please rotate your device\nto landscape mode";
            text.fontSize = 32;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            if (DEBUG_MODE)
                Debug.Log("[ForceLandscapeAspect] Rotate overlay created");
        }

        internal static void CreatePhoneIcon(GameObject parent)
        {
            // Phone body (portrait rectangle)
            GameObject body = new GameObject("Body");
            body.transform.SetParent(parent.transform, false);
            RectTransform bodyRect = body.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.sizeDelta = new Vector2(60, 100);

            Image bodyImg = body.AddComponent<Image>();
            bodyImg.color = Color.white;

            // Screen (inner dark rectangle)
            GameObject screen = new GameObject("Screen");
            screen.transform.SetParent(body.transform, false);
            RectTransform screenRect = screen.AddComponent<RectTransform>();
            screenRect.anchorMin = new Vector2(0.5f, 0.5f);
            screenRect.anchorMax = new Vector2(0.5f, 0.5f);
            screenRect.sizeDelta = new Vector2(50, 80);

            Image screenImg = screen.AddComponent<Image>();
            screenImg.color = new Color(0.2f, 0.2f, 0.2f);

            // Curved arrow indicating rotation
            GameObject arrow = new GameObject("Arrow");
            arrow.transform.SetParent(parent.transform, false);
            RectTransform arrowRect = arrow.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(140, 140);
            arrowRect.localRotation = Quaternion.Euler(0, 0, -45);

            // Create arrow using lines
            CreateArrowArc(arrow);
        }

        internal static void CreateArrowArc(GameObject parent)
        {
            // Create curved arrow segments
            Color arrowColor = new Color(0.3f, 0.7f, 1f); // Light blue

            for (int i = 0; i < 6; i++)
            {
                GameObject segment = new GameObject($"Segment{i}");
                segment.transform.SetParent(parent.transform, false);
                RectTransform segRect = segment.AddComponent<RectTransform>();
                segRect.anchorMin = new Vector2(0.5f, 0.5f);
                segRect.anchorMax = new Vector2(0.5f, 0.5f);

                float angle = i * 25f - 60f;
                float rad = angle * Mathf.Deg2Rad;
                float radius = 55f;

                segRect.anchoredPosition = new Vector2(
                    Mathf.Cos(rad) * radius,
                    Mathf.Sin(rad) * radius
                );
                segRect.sizeDelta = new Vector2(12, 12);

                Image segImg = segment.AddComponent<Image>();
                segImg.color = arrowColor;
            }

            // Arrow head
            GameObject arrowHead = new GameObject("ArrowHead");
            arrowHead.transform.SetParent(parent.transform, false);
            RectTransform headRect = arrowHead.AddComponent<RectTransform>();
            headRect.anchorMin = new Vector2(0.5f, 0.5f);
            headRect.anchorMax = new Vector2(0.5f, 0.5f);

            float endAngle = 5 * 25f - 60f;
            float endRad = endAngle * Mathf.Deg2Rad;
            headRect.anchoredPosition = new Vector2(
                Mathf.Cos(endRad) * 55f + 10f,
                Mathf.Sin(endRad) * 55f
            );
            headRect.sizeDelta = new Vector2(20, 20);
            headRect.localRotation = Quaternion.Euler(0, 0, -30);

            Image headImg = arrowHead.AddComponent<Image>();
            headImg.color = new Color(0.3f, 0.7f, 1f);
        }
    }
}
