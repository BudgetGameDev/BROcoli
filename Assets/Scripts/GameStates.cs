using UnityEngine;

public class GameStates : MonoBehaviour
{
    public int score = 0;
    public float gameTime = 0f;
    public bool IsGameOver => player != null && player.getGameOver();

    [SerializeField] private PlayerController player;

    private int lastSecond = 0;
    private int lastTenSecondMilestone = 0;

    void Start()
    {
        score = 0;
        gameTime = 0f;
    }

    void Update()
    {
        gameTime += Time.deltaTime;

        int currentSecond = Mathf.FloorToInt(gameTime);

        // +1 score every second
        if (currentSecond > lastSecond)
        {
            score += 1;
            lastSecond = currentSecond;
        }

        // +1 extra score every 10 seconds
        int tenSecondMilestone = currentSecond / 10;
        if (tenSecondMilestone > lastTenSecondMilestone)
        {
            score += 1;
            lastTenSecondMilestone = tenSecondMilestone;
        }
    }
}