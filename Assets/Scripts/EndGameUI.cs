using UnityEngine;
using UnityEngine.SceneManagement;

public class EndGameUI : MonoBehaviour
{
    public void Restart()
    {
        Debug.Log("Restarting game");
        SceneManager.LoadScene("Game");
    }

    public void GoToMainMenu()
    {
        Debug.Log("Going to main menu");
        SceneManager.LoadScene("MainMenuScene");
    }
}
