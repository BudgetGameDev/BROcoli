using TMPro;
using UnityEngine;

public class ScoreValue : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI scoreText;
    private GameStates gameStates;

    void Awake()
    {
        gameStates = FindFirstObjectByType<GameStates>();
        
        // Disable raycast target - this text doesn't need to receive clicks
        // and prevents MissingReferenceException if destroyed
        if (scoreText != null)
        {
            scoreText.raycastTarget = false;
        }
    }
    
    void Update()
    {
        if (scoreText != null && gameStates != null)
        {
            scoreText.text = gameStates.score.ToString();
        }
    }
}
