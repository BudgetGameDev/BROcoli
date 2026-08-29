using UnityEngine;

public class GameStates : MonoBehaviour
{
    public int score = 0;
    public float gameTime = 0f;
    public int EnemiesKilled { get; private set; }
    public bool IsGameOver => player != null && player.getGameOver();

    [SerializeField]
    private PlayerController player;

    private int lastSecond = 0;
    private int lastTenSecondMilestone = 0;

    void Start()
    {
        score = 0;
        gameTime = 0f;
        EnemiesKilled = 0;
        lastSecond = 0;
        lastTenSecondMilestone = 0;
    }

    void Update()
    {
        if (IsGameOver)
            return;

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

    public void RecordEnemyKilled()
    {
        EnemiesKilled++;
    }

    public static string FormatSurvivalTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int remainingSeconds = totalSeconds % 60;

        return hours > 0
            ? $"{hours}:{minutes:00}:{remainingSeconds:00}"
            : $"{minutes:00}:{remainingSeconds:00}";
    }
}
