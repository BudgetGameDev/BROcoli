using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class LevelUpScreen
    {
        private void SetupButtons()
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null)
                {
                    int index = i;
                    choiceButtons[i].onClick.RemoveAllListeners();
                    choiceButtons[i].onClick.AddListener(() => ChooseUpgrade(index));
                }
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(ConfirmSelectedUpgrade);
            }
        }

        public void Show(int newLevel, PlayerStats stats)
        {
            if (levelUpPanel == null)
            {
                Debug.LogWarning("[LevelUpScreen] Panel not assigned");
                return;
            }

            isShowing = true;
            playerStats = stats;
            selectedIndex = 0;
            hasPendingSelection = false;

            if (levelUpAudio != null)
            {
                levelUpAudio.PlayLevelUpSound();
            }

            if (levelText != null)
            {
                levelText.text = $"LEVEL {newLevel}";
            }

            // Generate 3 upgrade options - one might be a troll upgrade
            for (int i = 0; i < 3; i++)
            {
                // 25% chance for troll upgrade on each slot, higher at higher levels
                float trollChance = Mathf.Min(0.15f + newLevel * 0.02f, 0.35f);

                if (Random.value < trollChance)
                {
                    currentOptions[i] = UpgradeOption.GenerateTrollUpgrade(newLevel);
                }
                else
                {
                    currentOptions[i] = UpgradeOption.GenerateRandom(newLevel);
                }
                UpdateChoiceUI(i, currentOptions[i]);
            }

            EnsureEventSystemActive();

            levelUpPanel.SetActive(true);
            levelUpPanel.transform.SetAsLastSibling();
            Time.timeScale = 0f;
            UpdateConfirmButton();

            // Select first button for controller/keyboard navigation
            if (choiceButtons[0] != null)
            {
                EventSystem.current?.SetSelectedGameObject(choiceButtons[0].gameObject);
            }
        }

        private void UpdateChoiceUI(int index, UpgradeOption option)
        {
            if (index < 0 || index >= 3)
                return;

            Color rarityColor = option.GetRarityColor();

            // Troll upgrades get a special yellow/orange tint
            if (option.IsTrollUpgrade)
            {
                rarityColor = new Color(1f, 0.6f, 0.2f); // Orange for trade-offs
            }

            if (choiceRarityTexts[index] != null)
            {
                string rarityText = option.IsTrollUpgrade ? "TRADE-OFF" : option.GetRarityName();
                choiceRarityTexts[index].text = rarityText;
                choiceRarityTexts[index].color = rarityColor;
            }

            if (choiceNameTexts[index] != null)
            {
                choiceNameTexts[index].text = option.DisplayName;
            }

            if (choiceDescTexts[index] != null)
            {
                // Troll upgrades already have colored description
                if (option.IsTrollUpgrade)
                {
                    choiceDescTexts[index].text = option.Description;
                    choiceDescTexts[index].color = Color.white; // White base, colors in rich text
                }
                else
                {
                    choiceDescTexts[index].text = option.Description;
                    choiceDescTexts[index].color = rarityColor;
                }
            }

            if (choiceBackgrounds[index] != null)
            {
                // Darken the rarity color for background
                Color bgColor = rarityColor * 0.3f;
                bgColor.a = 0.9f;
                choiceBackgrounds[index].color = bgColor;
            }
        }

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
        /// <summary>
        /// Autoplay/E2E hook: programmatically pick an upgrade (mirrors a button click)
        /// so an unattended run never stalls on the paused level-up menu.
        /// </summary>
        public void AutoSelectUpgrade(int index) => ApplyUpgrade(index);

        /// <summary>Autoplay/E2E hook: number of upgrade options currently offered.</summary>
        public int OptionCount => currentOptions?.Length ?? 0;

        /// <summary>Autoplay/E2E hook: read an offered option so a bot can score/choose it.</summary>
        public UpgradeOption GetOption(int index) =>
            (currentOptions != null && index >= 0 && index < currentOptions.Length)
                ? currentOptions[index]
                : null;
#endif

        private void ChooseUpgrade(int index)
        {
            if (index < 0 || index >= currentOptions.Length)
                return;
            if (currentOptions[index] == null)
                return;

            SetSelectedIndex(index);
            hasPendingSelection = true;
            UpdateConfirmButton();
        }

        private void ConfirmSelectedUpgrade()
        {
            if (!hasPendingSelection)
                return;
            ApplyUpgrade(selectedIndex);
        }

        private void ApplyUpgrade(int index)
        {
            if (index < 0 || index >= currentOptions.Length)
                return;
            if (currentOptions[index] == null)
                return;
            if (playerStats == null)
                return;

            // Use hyped sound for level-up stat selection!
            ProceduralUIAudio.PlayLevelUpSelect();
            PlayerStats upgradedStats = playerStats;
            currentOptions[index].ApplyTo(upgradedStats);
            Hide();
            upgradedStats.CompleteLevelUpChoice();
        }

        public void Hide()
        {
            if (levelUpPanel != null)
            {
                levelUpPanel.SetActive(false);
            }

            Time.timeScale = 1f;
            isShowing = false;
            hasPendingSelection = false;
            playerStats = null;
            UpdateConfirmButton();
        }

        public bool IsShowing() => isShowing;

        public bool HasPendingSelection => hasPendingSelection;

        void Update()
        {
            if (!isShowing)
                return;

            // Handle gamepad/keyboard navigation
            HandleControllerNavigation();

            ProcessKeyboardShortcuts(
                Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1),
                Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2),
                Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)
            );

            // Update selection visuals
            UpdateSelectionVisuals();
        }
    }
}
