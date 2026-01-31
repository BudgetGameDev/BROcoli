using System;
using TMPro;
using UnityEngine;

public class Timer : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI timerText;
    private GameStates gameStates;

    void Awake()
    {
        gameStates = FindFirstObjectByType<GameStates>();
        
        // Disable raycast target - this text doesn't need to receive clicks
        // and prevents MissingReferenceException if destroyed
        if (timerText != null)
        {
            timerText.raycastTarget = false;
        }
    }
    
    void Update()
    {
        if (timerText != null && gameStates != null)
        {
            timerText.text = TimeSpan.FromSeconds(gameStates.gameTime).ToString(@"mm\:ss");
        }
    }
}
