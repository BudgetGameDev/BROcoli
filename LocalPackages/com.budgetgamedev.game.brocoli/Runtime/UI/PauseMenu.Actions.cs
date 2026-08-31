using System.Runtime.InteropServices;
using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class PauseMenu
    {
        private void HandleControllerNavigation()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            Vector2 dpad = gamepad?.dpad.ReadValue() ?? Vector2.zero;
            Vector2 stick = gamepad?.leftStick.ReadValue() ?? Vector2.zero;
            float vertical = ResolveVerticalInput(
                keyboard != null && (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed),
                keyboard != null && (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed),
                dpad.y,
                stick.y
            );
            bool submit =
                (
                    keyboard != null
                    && (
                        keyboard.enterKey.wasPressedThisFrame
                        || keyboard.spaceKey.wasPressedThisFrame
                    )
                ) || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            bool back = gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
            ProcessControllerNavigation(vertical, submit, back);
        }

        private void SelectMenuButton(int index)
        {
            if (menuButtons == null || index < 0 || index >= menuButtons.Length)
                return;

            // Play hover sound if index changed
            if (index != selectedButtonIndex)
            {
                ProceduralUIAudio.PlayHover();
            }

            selectedButtonIndex = index;

            if (EventSystem.current != null && menuButtons[index] != null)
            {
                EventSystem.current.SetSelectedGameObject(menuButtons[index].gameObject);
            }
        }

        private void UpdateSelectionVisuals()
        {
            if (menuButtons == null || buttonOutlines == null)
                return;

            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (menuButtons[i] == null)
                    continue;

                bool isSelected = (i == selectedButtonIndex);

                // Update outline
                if (
                    buttonOutlines != null
                    && i < buttonOutlines.Length
                    && buttonOutlines[i] != null
                )
                {
                    buttonOutlines[i].enabled = isSelected;
                }

                // Animate scale
                if (originalScales != null && i < originalScales.Length)
                {
                    RectTransform rt = menuButtons[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        float targetScale = isSelected ? MenuTheme.SelectedScale : 1f;
                        Vector3 target = originalScales[i] * targetScale;
                        rt.localScale = Vector3.Lerp(
                            rt.localScale,
                            target,
                            Time.unscaledDeltaTime * MenuTheme.SelectionLerpSpeed
                        );
                    }
                }
            }
        }

        public void TogglePause()
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }

        public void Pause()
        {
            if (pauseMenuUI == null)
            {
                Debug.LogError("[PauseMenu] pauseMenuUI is null!");
                return;
            }

            // Ensure EventSystem is active before showing menu
            EnsureEventSystemActive();

            // Force canvas to rebuild its graphic registry (fixes MissingReferenceException)
            RefreshCanvasGraphics();

            // Bring to front (last sibling = on top)
            pauseMenuUI.transform.SetAsLastSibling();

            // Show menu
            pauseMenuUI.SetActive(true);

            // Setup controller navigation
            SetupMenuNavigation();
            selectedButtonIndex = 0;
            SelectMenuButton(0);

            // Pause button visibility is managed by VirtualController

            // Pause game
            Time.timeScale = 0f;
            isPaused = true;
            GameAudioSettings.SetPauseMenuOpen(true);

            Debug.Log("[PauseMenu] Game PAUSED");
        }

        /// <summary>
        /// Forces Canvas to rebuild its internal graphic list, removing any destroyed references.
        /// This fixes MissingReferenceException in GraphicRaycaster.
        /// </summary>
        private void RefreshCanvasGraphics()
        {
            if (mainCanvas == null)
            {
                mainCanvas = ScreenCanvasLocator.Find();
            }

            if (mainCanvas != null)
            {
                // Get the GraphicRaycaster and force it to rebuild
                GraphicRaycaster raycaster = mainCanvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                {
                    // Disable and re-enable to force rebuild of graphic list
                    raycaster.enabled = false;
                    raycaster.enabled = true;
                }

                // Force canvas update
                Canvas.ForceUpdateCanvases();
            }
        }

        public void Resume()
        {
            if (pauseMenuUI == null)
            {
                Debug.LogError("[PauseMenu] pauseMenuUI is null!");
                return;
            }

            ProceduralUIAudio.PlaySelect();

            ResetMenuNavigation();

            // Hide menu
            pauseMenuUI.SetActive(false);

            // Pause button visibility is managed by VirtualController

            // Resume game
            Time.timeScale = 1f;
            isPaused = false;
            GameAudioSettings.SetPauseMenuOpen(false);

            Debug.Log("[PauseMenu] Game RESUMED");
        }

        public void GoToMainMenu() => GoToMainMenu(SceneManager.LoadScene);

        internal void GoToMainMenu(System.Action<string> loadScene)
        {
            Debug.Log("[PauseMenu] Going to MainMenuScene");
            BrocoliAutosaveController.SaveNow();
            // Reset time before loading
            Time.timeScale = 1f;
            isPaused = false;

            loadScene("Brocoli_MainMenu");
        }

        internal static Button FindNamedButton(Button[] buttons, params string[] names)
        {
            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;
                string objectName = button.gameObject.name.ToLowerInvariant();
                foreach (string name in names)
                    if (objectName.Contains(name))
                        return button;
            }
            return null;
        }

        public bool IsPaused() => isPaused;

        /// <summary>Shared input and focus handling drive pause through this.</summary>
        bool IPauseController.IsPaused => isPaused;

        // Re-check EventSystem when this component is enabled
        void OnEnable()
        {
            EnsureEventSystemActive();
        }
    }
}
