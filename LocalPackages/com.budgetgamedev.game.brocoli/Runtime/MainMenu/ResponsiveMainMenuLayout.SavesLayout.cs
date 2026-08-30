using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private const float SaveRowGap = 8f;

        private float saveRowHeight;
        private float saveRowStride;

        private void LayoutSavesPanel(
            float width,
            float top,
            float bottom,
            bool compact,
            bool narrow
        )
        {
            if (savesPanel == null)
                return;

            Stretch(savesPanel);
            float gap = compact ? 7f : 10f;
            float titleHeight = compact ? 24f : 32f;
            float hintHeight = compact ? 18f : 22f;
            float buttonHeight = compact ? 38f : 48f;

            savesTitle.fontSize = narrow ? 18f : (compact ? 19f : 22f);
            SetCenteredRect(savesTitle.rectTransform, width, titleHeight, top - titleHeight * 0.5f);

            savesHint.fontSize = narrow ? 12f : (compact ? 12f : 14f);
            SetCenteredRect(
                savesHint.rectTransform,
                width,
                hintHeight,
                top - titleHeight - hintHeight * 0.5f
            );

            // Four lines under the list: what to do with the picked run, the two
            // ways to start a new one, and the way back out.
            float footerHeight = buttonHeight * 4f + gap * 3f;
            float viewportTop = top - titleHeight - hintHeight - gap;
            float viewportBottom = bottom + footerHeight + gap;
            float viewportHeight = Mathf.Max(96f, viewportTop - viewportBottom);
            SetCenteredRect(
                savesViewport,
                width,
                viewportHeight,
                (viewportTop + viewportBottom) * 0.5f
            );

            float y = bottom + footerHeight - buttonHeight * 0.5f;
            LayoutSaveActionPair(width, buttonHeight, y, gap);

            y -= buttonHeight + gap;
            foreach (Button button in new[] { newRunButton, newTouchRunButton, backSavesButton })
            {
                SetCenteredRect(button.GetComponent<RectTransform>(), width, buttonHeight, y);
                y -= buttonHeight + gap;
            }

            foreach (Button button in savesActionButtons)
            {
                // The play buttons come from the scene at their own authored size, so
                // the panel sizes every action label the way the menu sizes its own.
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.fontSize = narrow ? (compact ? 14f : 16f) : (compact ? 16f : 18f);
            }

            LayoutSaveRows(savesViewport.rect.width, compact, narrow);
            Stretch(savesEmpty.rectTransform);
        }

        /// <summary>Play and Delete sit side by side on the line under the list.</summary>
        private void LayoutSaveActionPair(float width, float height, float y, float gap)
        {
            float deleteWidth = Mathf.Max(110f, width * 0.38f);
            float playWidth = Mathf.Max(110f, width - deleteWidth - gap);

            RectTransform play = playSaveButton.GetComponent<RectTransform>();
            SetCenteredRect(play, playWidth, height, y);
            play.anchoredPosition = new Vector2(-(width - playWidth) * 0.5f, y);

            RectTransform delete = deleteSaveButton.GetComponent<RectTransform>();
            SetCenteredRect(delete, deleteWidth, height, y);
            delete.anchoredPosition = new Vector2((width - deleteWidth) * 0.5f, y);
        }

        private void LayoutSaveRows(float viewportWidth, bool compact, bool narrow)
        {
            saveRowHeight = compact ? 58f : 70f;
            saveRowStride = saveRowHeight + SaveRowGap;

            float rowWidth = Mathf.Max(120f, viewportWidth - 16f);

            // The list hangs from the top of the viewport and grows downwards, so the
            // newest run is the first thing under the heading.
            savesContent.anchorMin = new Vector2(0f, 1f);
            savesContent.anchorMax = new Vector2(1f, 1f);
            savesContent.pivot = new Vector2(0.5f, 1f);
            savesContent.anchoredPosition = Vector2.zero;
            savesContent.sizeDelta = new Vector2(
                0f,
                Mathf.Max(0f, visibleSaveCount * saveRowStride + SaveRowGap)
            );

            for (int index = 0; index < saveRows.Count; index++)
            {
                SaveRow row = saveRows[index];
                if (!row.Root.gameObject.activeSelf)
                    continue;

                row.Root.anchorMin = new Vector2(0.5f, 1f);
                row.Root.anchorMax = new Vector2(0.5f, 1f);
                row.Root.pivot = new Vector2(0.5f, 1f);
                row.Root.sizeDelta = new Vector2(rowWidth, saveRowHeight);
                row.Root.anchoredPosition = new Vector2(0f, -(SaveRowGap + index * saveRowStride));

                RectTransform select = row.Select.GetComponent<RectTransform>();
                SetChildRect(select, rowWidth, saveRowHeight, 0f);

                row.Headline.fontSize = narrow ? 15f : (compact ? 16f : 18f);
                row.Detail.fontSize = narrow ? 11f : (compact ? 12f : 13f);

                // Headline on the upper half, the played/last-seen line under it.
                SetHalfRect(row.Headline.rectTransform, rowWidth, saveRowHeight, true);
                SetHalfRect(row.Detail.rectTransform, rowWidth, saveRowHeight, false);
            }
        }

        private static void SetChildRect(RectTransform rect, float width, float height, float x)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        private static void SetHalfRect(
            RectTransform rect,
            float width,
            float height,
            bool upperHalf
        )
        {
            float half = height * 0.5f;
            SetChildRect(rect, width, half, 0f);
            rect.anchoredPosition = new Vector2(0f, upperHalf ? half * 0.5f : -half * 0.5f);
        }

        /// <summary>Scrolls the list so the row the player is on stays on screen.</summary>
        private void EnsureSelectedRowVisible()
        {
            if (
                savesScroll == null
                || savesFocus >= visibleSaveCount
                || savesContent == null
                || saveRowStride <= 0f
            )
            {
                return;
            }

            float contentHeight = savesContent.rect.height;
            float viewHeight = savesViewport.rect.height;
            float scrollable = contentHeight - viewHeight;
            if (scrollable <= 0f)
                return;

            float rowTop = SaveRowGap + savesFocus * saveRowStride;
            float rowBottom = rowTop + saveRowHeight;
            float offset = (1f - savesScroll.verticalNormalizedPosition) * scrollable;

            if (rowTop < offset)
                offset = Mathf.Max(0f, rowTop - SaveRowGap);
            else if (rowBottom > offset + viewHeight)
                offset = rowBottom - viewHeight;
            else
                return;

            savesScroll.verticalNormalizedPosition = Mathf.Clamp01(1f - offset / scrollable);
        }
    }
}
