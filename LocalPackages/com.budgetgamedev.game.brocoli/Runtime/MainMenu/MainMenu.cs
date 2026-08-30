using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class MainMenu : MonoBehaviour
    {
        [Header("PWA Install Button (Optional)")]
        [Tooltip("Assign a button to show/hide based on PWA install status")]
        public GameObject installAppButton;

        [Header("Quit Button (Hidden on WebGL)")]
        [Tooltip("Assign the quit button to auto-hide on WebGL builds")]
        public GameObject quitButton;

        [Header("Menu Buttons")]
        [SerializeField]
        private Button[] menuButtons;

        private int selectedIndex = -1;

        private void Awake() => gameObject.AddComponent<ResponsiveMainMenuLayout>();

        void Start()
        {
            // Hide install button if already running as installed PWA
            if (installAppButton != null)
            {
                bool showInstallButton = !PWAHelper.IsInstalledAsPWA;
                installAppButton.SetActive(showInstallButton);

                if (PWAHelper.IsInstalledAsPWA)
                {
                    Debug.Log("[MainMenu] Running as installed PWA - hiding install button");
                }
            }

            // Hide quit button on WebGL - Application.Quit() doesn't work reliably in browsers
#if UNITY_WEBGL
            if (quitButton != null)
            {
                quitButton.SetActive(false);
                Debug.Log("[MainMenu] WebGL build - hiding quit button");
            }
#endif
            PWAHelper.LogStatus();

            // Setup controller navigation
            SuppressEventSystemNavigation();
            SetupControllerNavigation();
        }

        private void OnDestroy() => RestoreEventSystemNavigation();

        public void Update()
        {
            HandleMenuInput();
            UpdateSelectionVisuals();
        }

        /// <summary>
        /// Shows the PWA install wizard. Hook this up to an "Install App" button.
        /// </summary>
        public void ShowInstallPrompt()
        {
            Debug.Log("[MainMenu] Install App button pressed");
            PWAHelper.ShowInstallPrompt();
        }

        /// <summary>
        /// Toggles fullscreen mode. Useful for players who didn't install as PWA.
        /// </summary>
        public void ToggleFullscreen()
        {
            Debug.Log("[MainMenu] Fullscreen toggle pressed");
            PWAHelper.ToggleFullscreen();
        }

        /// <summary>Called by the New Run button in the save manager.</summary>
        public void playGame() => LaunchNewDungeon(false);

        /// <summary>Called by the New Run (touch) button in the save manager.</summary>
        public void playGameMobile() => LaunchNewDungeon(true);

        /// <summary>Resumes the run held in the given save slot.</summary>
        /// <returns>False when that slot turned out to be empty or unreadable.</returns>
        public bool LoadSave(int slot)
        {
            if (!BrocoliSaveSystem.BeginContinue(slot))
                return false;

            ProceduralUIAudio.PlaySelect();
            SceneManager.LoadScene("Brocoli_Dungeon");
            return true;
        }

        /// <summary>
        /// The dungeon is the only game mode, so Play starts it outright. The scene
        /// is named rather than reached by build index so reordering the build
        /// settings cannot silently launch something else.
        /// </summary>
        /// <returns>False when all ten save slots are taken.</returns>
        private static bool LaunchNewDungeon(bool mobileControls)
        {
            if (!BrocoliSaveSystem.BeginNewGame(mobileControls))
            {
                Debug.Log("[MainMenu] Every save slot is taken; delete one to start a new run.");
                return false;
            }

            ProceduralUIAudio.PlaySelect();
            SceneManager.LoadScene("Brocoli_Dungeon");
            return true;
        }

        public void GoToSettingsMenu()
        {
            Debug.Log("Settings has been pressed");
            SceneManager.LoadScene("SettingsMenuScene");
        }

        public void GoToMainMenu()
        {
            Debug.Log("Back has been pressed");
            SceneManager.LoadScene("Brocoli_MainMenu");
        }

        public void quitGame()
        {
            Debug.Log("Quit Game has been pressed");
            PWAHelper.Quit();
        }
    }
}
