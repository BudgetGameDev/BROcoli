using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    /// <summary>Called by the Play button.</summary>
    public void playGame() => LaunchDungeon(false);

    /// <summary>Called by the Play (touch) button.</summary>
    public void playGameMobile() => LaunchDungeon(true);

    /// <summary>
    /// The dungeon is the only game mode, so Play starts it outright. The scene
    /// is named rather than reached by build index so reordering the build
    /// settings cannot silently launch something else.
    /// </summary>
    private static void LaunchDungeon(bool mobileControls)
    {
        ProceduralUIAudio.PlaySelect();
        PlayerPrefs.SetInt("ShowVirtualController", mobileControls ? 1 : 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene("Dungeon");
    }

    public void GoToSettingsMenu()
    {
        Debug.Log("Settings has been pressed");
        SceneManager.LoadScene("SettingsMenuScene");
    }

    public void GoToMainMenu()
    {
        Debug.Log("Back has been pressed");
        SceneManager.LoadScene("MainMenuScene");
    }

    public void quitGame()
    {
        Debug.Log("Quit Game has been pressed");
        PWAHelper.Quit();
    }
}
