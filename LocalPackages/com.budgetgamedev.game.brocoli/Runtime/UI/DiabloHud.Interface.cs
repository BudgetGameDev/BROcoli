using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class DiabloHud
    {
        private void BuildInterface()
        {
            RectTransform canvasRoot = transform as RectTransform;
            if (canvasRoot == null)
                return;

            safeArea = CreateRect("DiabloHudSafeArea", canvasRoot);
            Stretch(safeArea);
            safeArea.SetAsFirstSibling();

            playerHealthBar = FindScreenBar("HealthBar");
            if (playerHealthBar != null)
            {
                playerHealthBar.transform.SetParent(safeArea, false);
                playerHealthBar.gameObject.SetActive(true);
                playerHealthSlider = StyleExistingBar(
                    playerHealthBar,
                    HealthBackground,
                    HealthFill
                );
                playerHealthLabel = AddBarLabel(playerHealthBar.transform, "HEALTH");
            }

            experienceBar = FindScreenBar("ExperienceBar");
            if (experienceBar != null)
            {
                experienceBar.transform.SetParent(safeArea, false);
                experienceBar.gameObject.SetActive(true);
                experienceSlider = StyleExistingBar(
                    experienceBar,
                    ExperienceBackground,
                    ExperienceFill
                );
                experienceLabel = AddBarLabel(experienceBar.transform, "LEVEL 1  ·  XP");
            }

            manaPanel = CreateResourcePanel(
                "ManaPlaceholder",
                safeArea,
                ManaBackground,
                ManaFill,
                out manaLabel
            );
            manaLabel.text = "MANA  ·  — / —";

            enemyPanel = CreatePanel("EnemyHealth", safeArea, EnemyBackground);
            AddOutline(enemyPanel.gameObject, new Color(0.82f, 0.64f, 0.38f, 0.7f));
            enemyFillRect = CreatePanel("Fill", enemyPanel, EnemyFill);
            enemyFill = enemyFillRect.GetComponent<Image>();
            enemyFill.type = Image.Type.Simple;
            enemyFillRect.anchorMin = Vector2.zero;
            enemyFillRect.anchorMax = Vector2.one;
            enemyFillRect.offsetMin = new Vector2(4f, 4f);
            enemyFillRect.offsetMax = new Vector2(-4f, -4f);

            enemyLabel = CreateText(
                "Label",
                enemyPanel,
                "ENEMY",
                20f,
                OnSurface,
                TMP_Settings.defaultFontAsset
            );
            Stretch(enemyLabel.rectTransform);
            enemyLabel.margin = new Vector4(14f, 0f, 14f, 0f);
            enemyPanel.gameObject.SetActive(false);
        }

        private Bar FindScreenBar(string objectName)
        {
            foreach (Bar bar in GetComponentsInChildren<Bar>(true))
            {
                if (bar.gameObject.name == objectName)
                    return bar;
            }

            return null;
        }

        private Slider StyleExistingBar(Bar bar, Color backgroundColor, Color fillColor)
        {
            RectTransform root = bar.transform as RectTransform;
            root.localScale = Vector3.one;
            root.localRotation = Quaternion.identity;

            Slider slider = bar.GetComponent<Slider>();
            if (slider != null)
            {
                slider.transition = Selectable.Transition.None;
                slider.navigation = new Navigation { mode = Navigation.Mode.None };
                slider.minValue = 0f;
                slider.maxValue = 1f;
            }

            Transform backgroundTransform = FindDescendant(root, "Background");
            Image background = backgroundTransform?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = null;
                background.type = Image.Type.Simple;
                background.color = backgroundColor;
                background.raycastTarget = false;
                Stretch(background.rectTransform);
                AddOutline(background.gameObject, Divider);
            }

            Transform fillTransform = FindDescendant(root, "Fill");
            Image fill = fillTransform?.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = null;
                fill.type = Image.Type.Simple;
                fill.color = fillColor;
                fill.raycastTarget = false;
            }

            Transform fillAreaTransform = FindDescendant(root, "Fill Area");
            if (fillAreaTransform is RectTransform fillArea)
            {
                fillArea.anchorMin = Vector2.zero;
                fillArea.anchorMax = Vector2.one;
                fillArea.offsetMin = new Vector2(4f, 4f);
                fillArea.offsetMax = new Vector2(-4f, -4f);
            }

            return slider;
        }

        private TMP_Text AddBarLabel(Transform parent, string initialValue)
        {
            RectTransform parentRect = parent as RectTransform;
            TMP_Text label = CreateText(
                "DiabloLabel",
                parentRect,
                initialValue,
                16f,
                OnSurface,
                TMP_Settings.defaultFontAsset
            );
            Stretch(label.rectTransform);
            label.margin = new Vector4(10f, 0f, 10f, 0f);
            label.characterSpacing = 1f;
            return label;
        }

        private static RectTransform CreateResourcePanel(
            string objectName,
            RectTransform parent,
            Color backgroundColor,
            Color fillColor,
            out TMP_Text label
        )
        {
            RectTransform panel = CreatePanel(objectName, parent, backgroundColor);
            AddOutline(panel.gameObject, Divider);

            RectTransform fill = CreatePanel("Fill", panel, fillColor);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);

            label = CreateText(
                "Label",
                panel,
                string.Empty,
                16f,
                OnSurface,
                TMP_Settings.defaultFontAsset
            );
            Stretch(label.rectTransform);
            label.margin = new Vector4(10f, 0f, 10f, 0f);
            label.characterSpacing = 1f;
            return panel;
        }

        private static void AddOutline(GameObject target, Color color)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
                outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }
    }
}
