using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Hub
{
    public sealed partial class GameLauncher
    {
        private static readonly Color Background = new(0.08f, 0.09f, 0.11f);
        private static readonly Color Panel = new(0.14f, 0.15f, 0.18f);
        private static readonly Color RowIdle = new(0.19f, 0.20f, 0.24f);
        private static readonly Color RowSelected = new(0.28f, 0.42f, 0.62f);
        private static readonly Color Ink = new(0.92f, 0.93f, 0.95f);
        private static readonly Color InkMuted = new(0.62f, 0.65f, 0.70f);

        private void BuildInterface()
        {
            Canvas canvas = ScreenCanvasLocator.GetOrCreate();
            RectTransform root = CreateRect("Launcher", canvas.transform);
            Stretch(root, Vector2.zero, Vector2.one);
            AddImage(root, Background);

            RectTransform panel = CreateRect("Panel", root);
            Stretch(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.sizeDelta = new Vector2(760f, 620f);
            AddImage(panel, Panel);

            CreateHeading(panel);
            CreateScrollList(panel);
            CreateSelectButton(panel);
            CreateEmptyLabel(panel);
        }

        private void CreateHeading(RectTransform panel)
        {
            RectTransform heading = CreateRect("Heading", panel);
            Stretch(heading, new Vector2(0f, 1f), new Vector2(1f, 1f));
            heading.pivot = new Vector2(0.5f, 1f);
            heading.anchoredPosition = new Vector2(0f, -28f);
            heading.sizeDelta = new Vector2(-64f, 44f);
            Text title = AddText(heading, "Select a game", 30, Ink, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
        }

        /// <summary>
        /// A ScrollRect over a vertical list. The list scrolls rather than shrinking
        /// rows, so the launcher reads the same with two games as with twenty.
        /// </summary>
        private void CreateScrollList(RectTransform panel)
        {
            RectTransform viewport = CreateRect("Viewport", panel);
            Stretch(viewport, Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(32f, 104f);
            viewport.offsetMax = new Vector2(-32f, -88f);
            AddImage(viewport, new Color(0.10f, 0.11f, 0.13f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            listContent = CreateRect("Content", viewport);
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = listContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
        }

        private void CreateSelectButton(RectTransform panel)
        {
            RectTransform holder = CreateRect("Select", panel);
            holder.anchorMin = new Vector2(0.5f, 0f);
            holder.anchorMax = new Vector2(0.5f, 0f);
            holder.pivot = new Vector2(0.5f, 0f);
            holder.anchoredPosition = new Vector2(0f, 32f);
            holder.sizeDelta = new Vector2(280f, 56f);

            Image face = AddImage(holder, RowSelected);
            selectButton = holder.gameObject.AddComponent<Button>();
            selectButton.targetGraphic = face;
            selectButton.onClick.AddListener(LaunchSelected);

            RectTransform label = CreateRect("Label", holder);
            Stretch(label, Vector2.zero, Vector2.one);
            AddText(label, "Select", 22, Ink, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
        }

        private void CreateEmptyLabel(RectTransform panel)
        {
            RectTransform holder = CreateRect("Empty", panel);
            Stretch(holder, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            holder.sizeDelta = new Vector2(-64f, 80f);
            emptyLabel = AddText(
                holder,
                "No games are installed.\nAdd a game package to Packages/manifest.json.",
                18,
                InkMuted,
                TextAnchor.MiddleCenter
            );
            emptyLabel.gameObject.SetActive(false);
        }
    }
}
