using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main-menu entry point for the dungeon-crawler game mode. Lives beside
/// <see cref="MainMenu"/> so the existing wave-mode buttons stay untouched.
/// </summary>
public class DungeonMenuAction : MonoBehaviour
{
    /// <summary>Called by the main menu's Dungeon button.</summary>
    public void playDungeon()
    {
        PlayerPrefs.SetInt("ShowVirtualController", Application.isMobilePlatform ? 1 : 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene("Dungeon");
    }
}
