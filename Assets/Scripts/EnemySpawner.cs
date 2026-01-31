using System;
using System.Linq;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Rate Curve (percentage-based)")]
    public float baseSpawnInterval = 1f;
    public float intervalMultiplier = 0.85f;
    public float stepSeconds = 5f;
    public float minSpawnInterval = 0.1f;

    public float currentInterval;

    private GameStates gameStates;
    private float nextSpawnTime;

    [Header("Spawn Settings")]
    public Transform player;
    public float spawnDistance = 10f;

    [Header("Wave")]
    [SerializeField] private WaveConfig waveConfig;
    private float waveStartGameTime;

    void Start()
    {
        gameStates = FindFirstObjectByType<GameStates>();
        nextSpawnTime = 0f;

        // If you set a wave config in inspector, treat Start as wave start.
        waveStartGameTime = gameStates != null ? gameStates.gameTime : 0f;
    }

    void Update()
    {
        if (gameStates == null || player == null) return;

        float curTime = gameStates.gameTime;
        float elapsedInWave = curTime - waveStartGameTime;

        float interval = GetCurrentInterval(elapsedInWave);
        currentInterval = interval;

        if (curTime >= nextSpawnTime)
        {
            SpawnEnemy(elapsedInWave);
            nextSpawnTime = curTime + interval;
        }
    }

    public void StartWave(WaveConfig config, float currentGameTime)
    {
        waveConfig = config;
        waveStartGameTime = currentGameTime;
        nextSpawnTime = currentGameTime;

        if (waveConfig != null && waveConfig.overrideSpawnerValues)
        {
            baseSpawnInterval = waveConfig.baseSpawnInterval;
            intervalMultiplier = waveConfig.intervalMultiplier;
            stepSeconds = waveConfig.stepSeconds;
            minSpawnInterval = waveConfig.minSpawnInterval;
        }

        currentInterval = baseSpawnInterval;
    }

    float GetCurrentInterval(float elapsedGlobal)
    {
        float scaled = baseSpawnInterval * Mathf.Pow(intervalMultiplier, elapsedGlobal / stepSeconds);
        return Mathf.Max(minSpawnInterval, scaled);
    }

    void SpawnEnemy(float elapsedInWave)
    {
        if (waveConfig == null || waveConfig.enemies == null || waveConfig.enemies.Length == 0) return;

        // Filter enemies allowed at this time in the wave
        var valid = waveConfig.enemies
            .Where(e => e.prefab != null)
            .Where(e => elapsedInWave >= e.startAfterSeconds)
            .Where(e => e.endAfterSeconds <= 0f || elapsedInWave <= e.endAfterSeconds)
            .Where(e => e.weight > 0)
            .ToArray();

        if (valid.Length == 0) return;

        GameObject prefab = PickWeighted(valid);
        Vector2 spawnPosition = GetPositionAroundPlayer();
        Instantiate(prefab, spawnPosition, Quaternion.identity);
    }

    GameObject PickWeighted(WaveConfig.EnemyEntry[] entries)
    {
        int total = 0;
        for (int i = 0; i < entries.Length; i++) total += entries[i].weight;

        int roll = UnityEngine.Random.Range(0, total);
        int acc = 0;

        for (int i = 0; i < entries.Length; i++)
        {
            acc += entries[i].weight;
            if (roll < acc) return entries[i].prefab;
        }

        return entries[entries.Length - 1].prefab;
    }

    Vector2 GetPositionAroundPlayer()
    {
        Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
        return (Vector2)player.position + direction * spawnDistance;
    }
}
