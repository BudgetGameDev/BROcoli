using UnityEngine;
using UnityEngine.SceneManagement;

public class OfficeScene : MonoBehaviour
{
    public void gotit()
    {
        Debug.Log("Back has been pressed");
        SceneManager.LoadScene("gw3");
    }
    
}
