using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverScreen : MonoBehaviour
{
    private bool open;
    public bool getOpen() {
        return open;
    }

    public void Start() {
        open = false;
    }
    
    public void Setup()
    {
        open = true;
        gameObject.SetActive(true);
        //Time.timeScale = 0;
    }

    public void Endurbyrja()
    {
        Debug.Log("restart");

        //Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        

    }

    public void Enda()
    {
        Debug.Log("exit");

        //Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);

    }

    public void Update() {
        if (Input.anyKey) {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
        }
        // if (Input.GetKey(KeyCode.Space)) {
        //     SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
        // }
    }
}
