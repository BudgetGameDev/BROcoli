using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pooling;

public class EnemySpawner : MonoBehaviour
{
    public Transform player;

    public bool IsWaveComplete { get; private set; }

    [Header("Powerup Drops")]
    [SerializeField] private GameObject[] powerupPrefabs;
    [SerializeField] private float powerupDropChance = 0.15f; // 15% chance per enemy kill
    
    [Header("Spawn Distribution")]
    [SerializeField] private float minSpawnDistance = 8f;
    [SerializeField] private float maxSpawnDistance = 14f;
    [SerializeField] private float spawnIntervalVariation = 0.5f; // ±50% of base interval
    [SerializeField] private float segmentJitterDegrees = 15f; // Random jitter within segment
    
    private int aliveEnemies;
    private WaveConfig currentWave;
    private bool hasPowerupDroppedThisWave = false;

    public event Action OnWaveCompleted;

    /// <summary>
    /// Set the powerup prefabs that can drop from enemies
    /// </summary>
    public void SetPowerupPrefabs(GameObject[] prefabs)
    {
        powerupPrefabs = prefabs;
    }

    public void StartWave(WaveConfig config)
    {
        if (config == null)
        {
            Debug.LogError("EnemySpawner: WaveConfig is null.");
            return;
        }

        currentWave = config;
        IsWaveComplete = false;
        aliveEnemies = 0;
        hasPowerupDroppedThisWave = false;

        Debug.Log($"EnemySpawner: Starting wave with {currentWave.enemyCount} enemies.");

        StopAllCoroutines();
        StartCoroutine(SpawnRoutineSegmented());
    }

    /// <summary>
    /// Segment-based spawn routine: divides spawn circle into segments,
    /// spawns one enemy per segment with randomized timing and distance.
    /// </summary>
    private IEnumerator SpawnRoutineSegmented()
    {
        int enemyCount = currentWave.enemyCount;
        float baseInterval = currentWave.spawnInterval;
        
        // Calculate angular segment size (divide circle into enemyCount segments)
        float segmentSize = 360f / enemyCount;
        
        // Shuffle spawn order to distribute enemy types across the wave
        List<int> spawnOrder = new List<int>(enemyCount);
        for (int i = 0; i < enemyCount; i++) spawnOrder.Add(i);
        ShuffleList(spawnOrder);
        
        for (int i = 0; i < enemyCount; i++)
        {
            int segmentIndex = spawnOrder[i];
            SpawnEnemyInSegment(segmentIndex, segmentSize);
            
            // Randomized interval: base ± variation
            float minInterval = baseInterval * (1f - spawnIntervalVariation);
            float maxInterval = baseInterval * (1f + spawnIntervalVariation);
            float delay = UnityEngine.Random.Range(minInterval, maxInterval);
            
            yield return new WaitForSeconds(delay);
        }
    }

    /// <summary>
    /// Spawn an enemy within a specific angular segment around the player.
    /// </summary>
    private void SpawnEnemyInSegment(int segmentIndex, float segmentSize)
    {
        if (player == null) return;
        
        // Select random prefab
        GameObject prefab = currentWave.enemyPrefabs[
            UnityEngine.Random.Range(0, currentWave.enemyPrefabs.Length)
        ];
        
        // Calculate angle: center of segment + random jitter
        float baseAngle = segmentIndex * segmentSize;
        float jitter = UnityEngine.Random.Range(-segmentJitterDegrees, segmentJitterDegrees);
        float angle = (baseAngle + jitter) * Mathf.Deg2Rad;
        
        // Randomized spawn distance
        float distance = UnityEngine.Random.Range(minSpawnDistance, maxSpawnDistance);
        
        // Calculate spawn position
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        Vector2 spawnPos = (Vector2)player.position + offset;
        
        // Try to get from pool first
        EnemyBase enemyPrefabComponent = prefab.GetComponent<EnemyBase>();
        EnemyBase enemy = null;
        
        if (enemyPrefabComponent != null)
        {
            enemy = PoolManager.Instance?.GetEnemy(enemyPrefabComponent, spawnPos, Quaternion.identity);
        }
        
        // Fallback to instantiate if pool not available
        if (enemy == null)
        {
            GameObject enemyObj = Instantiate(prefab, spawnPos, Quaternion.identity);
            enemy = enemyObj.GetComponent<EnemyBase>();
        }
        else
        {
            // Mark as pooled for proper death handling
            enemy.SetPooled(true);
            enemy.ResetForPool();
        }
        
        if (enemy != null)
        {
            aliveEnemies++;
            enemy.OnDeath += HandleEnemyDeath;
        }
    }

    private void HandleEnemyDeath(EnemyBase enemy)
    {
        enemy.OnDeath -= HandleEnemyDeath;
        
        // Try to drop a powerup (max 1 per wave)
        TryDropPowerup(enemy.transform.position);
        
        aliveEnemies--;

        if (aliveEnemies <= 0)
        {
            IsWaveComplete = true;
            OnWaveCompleted?.Invoke();
        }
    }
    
    private void TryDropPowerup(Vector3 position)
    {
        // Only one powerup can drop per wave
        if (hasPowerupDroppedThisWave) return;
        
        // Check if we have any powerup prefabs
        if (powerupPrefabs == null || powerupPrefabs.Length == 0) return;
        
        // Roll for drop chance
        if (UnityEngine.Random.value > powerupDropChance) return;
        
        // Drop a random powerup
        GameObject prefab = powerupPrefabs[UnityEngine.Random.Range(0, powerupPrefabs.Length)];
        Instantiate(prefab, position, Quaternion.identity);
        hasPowerupDroppedThisWave = true;
        
        Debug.Log($"Powerup dropped at {position}!");
    }
    
    /// <summary>
    /// Fisher-Yates shuffle for randomizing spawn order.
    /// </summary>
    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
