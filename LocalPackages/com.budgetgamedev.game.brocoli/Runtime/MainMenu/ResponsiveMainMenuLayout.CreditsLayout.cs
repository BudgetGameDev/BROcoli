using TMPro;
using UnityEngine;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private void LayoutCreditsPanel(
            float width,
            float top,
            float bottom,
            bool compact,
            bool narrow
        )
        {
            if (creditsPanel == null)
                return;

            Stretch(creditsPanel);
            float gap = compact ? 8f : 12f;
            float titleHeight = compact ? 24f : 32f;
            float buttonHeight = compact ? 42f : 54f;
            creditsTitle.fontSize = narrow ? 18f : (compact ? 19f : 22f);
            SetCenteredRect(
                creditsTitle.rectTransform,
                width,
                titleHeight,
                top - titleHeight * 0.5f
            );

            float viewportTop = top - titleHeight - gap;
            float viewportBottom = bottom + buttonHeight + gap;
            SetCenteredRect(
                creditsViewport,
                width,
                Mathf.Max(80f, viewportTop - viewportBottom),
                (viewportTop + viewportBottom) * 0.5f
            );
            SetCenteredRect(
                backCreditsButton.GetComponent<RectTransform>(),
                width,
                buttonHeight,
                bottom + buttonHeight * 0.5f
            );

            creditsBody.fontSize = narrow ? 16f : (compact ? 17f : 18f);
            RectTransform body = creditsBody.rectTransform;
            body.anchorMin = new Vector2(0f, 1f);
            body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.anchoredPosition = Vector2.zero;
            body.sizeDelta = new Vector2(0f, 0f);
            creditsBody.ForceMeshUpdate();
            body.sizeDelta = new Vector2(
                0f,
                Mathf.Max(creditsViewport.rect.height, creditsBody.preferredHeight)
            );
        }
    }
}
