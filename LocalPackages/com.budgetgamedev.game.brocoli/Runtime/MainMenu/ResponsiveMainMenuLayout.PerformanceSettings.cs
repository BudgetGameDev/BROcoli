using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private Button performanceButton;

        private void BuildPerformanceSetting()
        {
            settingsRows[4] = CreateRect("PerformanceRow", settingsPanel);
            performanceButton = CreateButton(
                "PerformanceOverlayToggle",
                settingsRows[4],
                PerformanceOverlay.ToggleLabel
            );
            StyleButton(performanceButton, false, materialFont);
            performanceButton.onClick.AddListener(() =>
            {
                PerformanceOverlay.Visible = !PerformanceOverlay.Visible;
                SyncPerformanceSetting();
            });
        }

        private void SyncPerformanceSetting()
        {
            if (performanceButton != null)
                performanceButton.GetComponentInChildren<TMP_Text>().text =
                    PerformanceOverlay.ToggleLabel;
        }

        private void LayoutPerformanceSetting(float width, float height)
        {
            SetCenteredRect(
                (RectTransform)performanceButton.transform,
                width,
                Mathf.Min(height, 38),
                0
            );
            performanceButton.GetComponentInChildren<TMP_Text>().fontSize = 14;
            SyncPerformanceSetting();
        }
    }
}
