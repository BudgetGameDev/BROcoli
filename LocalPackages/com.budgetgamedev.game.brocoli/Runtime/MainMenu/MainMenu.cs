using System;
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
                ConfigureInstallButton(PWAHelper.IsInstalledAsPWA);

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

        internal void ConfigureInstallButton(bool installed)
        {
            if (installAppButton == null)
                return;
            installAppButton.SetActive(!installed);
            if (installed)
                Debug.Log("[MainMenu] Running as installed PWA - hiding install button");
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
        public bool LoadSave(int slot) =>
            LoadSave(slot, () => SceneManager.LoadScene("Brocoli_Dungeon"));

        internal bool LoadSave(int slot, Action loadScene)
        {
            if (!BrocoliSaveSystem.BeginContinue(slot))
                return false;

            AutoplayFeatureLog.Record(AutoplayFeatures.MainMenuContinue);
            ProceduralUIAudio.PlaySelect();
            loadScene();
            return true;
        }

        /// <summary>
        /// The dungeon is the only game mode, so Play starts it outright. The scene
        /// is named rather than reached by build index so reordering the build
        /// settings cannot silently launch something else.
        /// </summary>
        /// <returns>False when all ten save slots are taken.</returns>
        private static bool LaunchNewDungeon(bool mobileControls) =>
            LaunchNewDungeon(mobileControls, () => SceneManager.LoadScene("Brocoli_Dungeon"));

        internal static bool LaunchNewDungeon(bool mobileControls, Action loadScene)
        {
            if (!BrocoliSaveSystem.BeginNewGame(mobileControls))
            {
                Debug.Log("[MainMenu] Every save slot is taken; delete one to start a new run.");
                return false;
            }

            AutoplayFeatureLog.Record(AutoplayFeatures.MainMenuNewGame);
            ProceduralUIAudio.PlaySelect();
            loadScene();
            return true;
        }

        public void quitGame()
        {
            Debug.Log("Quit Game has been pressed");
            PWAHelper.Quit();
        }
    }
}
