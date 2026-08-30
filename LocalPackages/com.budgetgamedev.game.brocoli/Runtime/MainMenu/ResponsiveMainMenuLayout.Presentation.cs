using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private void StyleButtons(Button[] buttons)
        {
            foreach (Button button in buttons)
            {
                bool primaryAction = button != null && button.name is "PlayButton";
                StyleButton(button, primaryAction, materialFont);
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
            return MenuTheme.CreateText(objectName, parent, value, fontSize, color, materialFont);
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

        private int GetVisibilitySignature()
        {
            int signature = 0;
            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] != null && mainButtons[i].gameObject.activeInHierarchy)
                    signature |= 1 << i;
            }
            return signature;
        }
    }
}
