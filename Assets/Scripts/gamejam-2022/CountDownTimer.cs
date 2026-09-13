using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CountDownTimer : MonoBehaviour { 

    public GameOverScreen GameOverScreen;
    public GameObject Player;
    public PlayerController playerController;

    [SerializeField] TextMeshProUGUI countdown;

    void GameOver()
    {
        GameOverScreen.Setup();
    }


    private void Start()
    {

    }

    private void Update()
    {
        // currentTime -= 1 * Time.unscaledDeltaTime;
        // //Debug.Log(currentTime);
        // //countdown.text = currentTime.ToString();
        // countdown.text = $"{playerController.getLength().ToString()} M";

        // if (playerController.getGameOver()) {
        //     GameOver();
        // }

        // if (Player)
        // {
        //     currentTime = 0;
        //     return;
        // }


        // if (currentTime <= 0)
        // {
        //     currentTime = 0;
        //     // Player = GameObject.Find("Player");
        //     // Player.SetActive(false);

        //     //GameOver();
        //     // SceneManager.LoadScene("questions");
        // }
    }
}
