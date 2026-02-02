using System.Collections;
using UnityEngine;

public class WaveGenerator : MonoBehaviour
{
    private static WaitForSeconds _waitForSeconds1 = new(1f);

    [SerializeField] private float preWaveCountdownSeconds = 5f;
    [SerializeField] private EnemySpawner enemySpawnerPrefab;
    [SerializeField] private Transform spawnerParent;
    [SerializeField] private Transform player;
    [SerializeField] private GameStates gameStates;
    [SerializeField] private WaveConfig[] waves;
    
    [Header("Powerup Drops")]
    [SerializeField] private GameObject[] powerupPrefabs;
    
    [Header("Victory Settings")]
    [SerializeField] private int victoryWave = 15;
    [SerializeField] private VictoryScreen victoryScreenPrefab;

    private EnemySpawner spawner;
    private int currentWave = 1;
    private bool isInfiniteMode = false;
    private VictoryScreen victoryScreen;

    public int CurrentWaveNumber => currentWave;
    public bool IsInfiniteMode => isInfiniteMode;

    private void Start()
    {
        spawner = Instantiate(
            enemySpawnerPrefab,
            spawnerParent != null ? spawnerParent : transform
        );
        spawner.player = player;
        
        if (powerupPrefabs != null && powerupPrefabs.Length > 0)
        {
            spawner.SetPowerupPrefabs(powerupPrefabs);
        }

        StartCoroutine(WaveLoop());
    }

    private IEnumerator WaveLoop()
    {
        while (gameStates == null || gameStates.IsGameOver == false)
        {
            yield return StartCoroutine(PreWaveCountdown());

            WaveConfig config = GetWaveConfig(currentWave);

            Debug.Log($"Starting Wave {currentWave}...");

            spawner.StartWave(config, currentWave);

            yield return new WaitUntil(() => spawner.IsWaveComplete);

            // Check for victory at wave 15
            if (currentWave == victoryWave && !isInfiniteMode)
            {
                yield return StartCoroutine(ShowVictoryScreen());
            }

            currentWave++;
        }
    }

    private IEnumerator ShowVictoryScreen()
    {
        // Create victory screen if needed
        if (victoryScreen == null)
        {
            if (victoryScreenPrefab != null)
            {
                victoryScreen = Instantiate(victoryScreenPrefab);
            }
            else
            {
                // Create dynamically
                GameObject vsObj = new GameObject("VictoryScreen");
                victoryScreen = vsObj.AddComponent<VictoryScreen>();
            }
        }
        
        bool decided = false;
        
        victoryScreen.OnContinueToInfinite += () => {
            isInfiniteMode = true;
            decided = true;
        };
        
        victoryScreen.OnEndRun += () => {
            decided = true;
        };
        
        victoryScreen.Show();
        
        // Wait for player decision
        yield return new WaitUntil(() => decided);
    }

    private IEnumerator PreWaveCountdown()
    {
        for (int i = Mathf.CeilToInt(preWaveCountdownSeconds); i > 0; i--)
        {
            Debug.Log($"Wave {currentWave} starts in {i}...");
            yield return _waitForSeconds1;
        }
    }

    private WaveConfig GetWaveConfig(int waveNumber)
    {
        if (waves == null || waves.Length == 0)
            return null;

        int index = Mathf.Clamp(waveNumber - 1, 0, waves.Length - 1);
        return waves[index];
    }
}
