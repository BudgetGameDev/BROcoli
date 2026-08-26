using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The game-mode selector (issue #46): Play opens a Waves / Dungeon choice
/// instead of starting a run directly. Lives beside <see cref="MainMenu"/>;
/// the Play buttons only remember whether mobile controls were requested and
/// the chosen mode then loads its scene with that preference.
/// </summary>
public class GameModeMenu : MonoBehaviour
{
    [SerializeField]
    private GameObject modePanel;

    [SerializeField]
    private GameObject[] mainButtons;

    [SerializeField]
    private Button firstModeButton;

    /// <summary>True while the Waves/Dungeon panel is showing.</summary>
    public static bool IsOpen { get; private set; }

    private bool mobileControls;
    private bool[] mainButtonsWereActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        IsOpen = false;
    }

    private void Awake()
    {
        IsOpen = false;
        if (modePanel != null)
            modePanel.SetActive(false);
    }

    /// <summary>Called by the Play button.</summary>
    public void openModeSelect()
    {
        mobileControls = false;
        ShowPanel(true);
    }

    /// <summary>Called by the Play (mobile) button.</summary>
    public void openModeSelectMobile()
    {
        mobileControls = true;
        ShowPanel(true);
    }

    /// <summary>Called by the Back button on the mode panel.</summary>
    public void backToMain()
    {
        ShowPanel(false);
    }

    /// <summary>Called by the Waves button on the mode panel.</summary>
    public void playWaves()
    {
        Launch("Game");
    }

    /// <summary>Called by the Dungeon button on the mode panel.</summary>
    public void playDungeon()
    {
        Launch("Dungeon");
    }

    private void Launch(string sceneName)
    {
        ProceduralUIAudio.PlaySelect();
        PlayerPrefs.SetInt("ShowVirtualController", mobileControls ? 1 : 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(sceneName);
    }

    private void ShowPanel(bool open)
    {
        ProceduralUIAudio.PlaySelect();
        IsOpen = open;

        if (open)
        {
            // Remember which main buttons were showing; PWA/WebGL rules hide
            // some of them, and closing the panel must not bring those back.
            mainButtonsWereActive = new bool[mainButtons.Length];
            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] == null)
                    continue;
                mainButtonsWereActive[i] = mainButtons[i].activeSelf;
                mainButtons[i].SetActive(false);
            }
        }

        if (modePanel != null)
            modePanel.SetActive(open);

        if (!open && mainButtonsWereActive != null)
        {
            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] != null)
                    mainButtons[i].SetActive(mainButtonsWereActive[i]);
            }
        }

        // Rebuild MainMenu's cached keyboard/controller navigation over the
        // buttons that are now visible.
        MainMenu mainMenu = GetComponent<MainMenu>();
        if (mainMenu != null)
            mainMenu.SetupControllerNavigation(true);

        if (open && firstModeButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(firstModeButton.gameObject);
    }
}
