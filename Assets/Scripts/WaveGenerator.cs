using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class WaveGenerator : MonoBehaviour
{
    public float waveIntervalSeconds = 30f;
    public float preWaveCountdownSeconds = 3f;
    public string waveText = null;

    public EnemySpawner enemySpawnerPrefab;
    public Transform spawnerParent;
    public Transform player;

    [Header("Wave List (Level 1 = index 0)")]
    public WaveConfig[] waves;

    private int currentWave = 0;
    private EnemySpawner activeSpawner;
    private GameStates gameStates;


    void Start()
    {
        gameStates = FindFirstObjectByType<GameStates>();
        StartCoroutine(WaveLoop());
    }

    IEnumerator WaveLoop()
    {
        while (true)
        {
            currentWave++;

            // Countdown
            for (int i = Mathf.CeilToInt(preWaveCountdownSeconds); i > 0; i--)
            {
                waveText = $"Wave {currentWave} starts in {i}...";
                yield return new WaitForSeconds(1f);
            }
            waveText = null;
            StartWave(currentWave);
            float remaining = Mathf.Max(0f, waveIntervalSeconds);
            yield return new WaitForSeconds(remaining);
        }
    }

    void StartWave(int waveNumber)
    {
        if (enemySpawnerPrefab == null)
        {
            Debug.LogError("WaveGenerator: enemySpawnerPrefab not assigned.");
            return;
        }

        // choose config (if you run out, keep using last)
        WaveConfig config = null;
        if (waves != null && waves.Length > 0)
        {
            int idx = Mathf.Clamp(waveNumber - 1, 0, waves.Length - 1);
            config = waves[idx];
        }

        if (activeSpawner)
        {
            Destroy(activeSpawner.gameObject);
        }
        
        Transform parent = spawnerParent != null ? spawnerParent : transform;
        activeSpawner = Instantiate(enemySpawnerPrefab, parent.position, parent.rotation, parent);

        activeSpawner.player = player;

        float curGameTime = gameStates != null ? gameStates.gameTime : Time.time;
        activeSpawner.StartWave(config, curGameTime);

        Debug.Log($"Wave {waveNumber} started with config: {(config != null ? config.name : "NONE")}");
    }
}
