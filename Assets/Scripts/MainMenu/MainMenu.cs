using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class MainMenu : MonoBehaviour
{
    [Header("PWA Install Button (Optional)")]
    [Tooltip("Assign a button to show/hide based on PWA install status")]
    public GameObject installAppButton;
    
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
        
        PWAHelper.LogStatus();
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
  
    public void playGame()
    {
        Debug.Log("Play Game has been pressed - Virtual Controller HIDDEN");
        PlayerPrefs.SetInt("ShowVirtualController", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
    
    public void playGameMobile()
    {
        Debug.Log("Play Game (Mobile) has been pressed - Virtual Controller SHOWN");
        PlayerPrefs.SetInt("ShowVirtualController", 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
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

    public void Update() {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
    }
}
