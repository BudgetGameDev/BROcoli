using System;
using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public Transform player;

    public bool IsWaveComplete { get; private set; }

    [Header("Powerup Drops")]
    [SerializeField] private GameObject[] powerupPrefabs;
    [SerializeField] private float powerupDropChance = 0.15f;
    
    [Header("Elite Settings")]
    [SerializeField] private float eliteChance = 0.05f;
    [SerializeField] private int minWaveForElites = 3;
    
    [Header("Infinite Mode Scaling")]
    [SerializeField] private int infiniteModeStartWave = 16;
    [SerializeField] private float infiniteHpScalePerWave = 0.25f;
    [SerializeField] private float infiniteSpeedScalePerWave = 0.02f;
    [SerializeField] private float infiniteCountScalePerWave = 0.15f;
    
    private int aliveEnemies;
    private WaveConfig currentWave;
    private bool hasPowerupDroppedThisWave = false;
    private int actualEnemyCount;
    private int currentWaveNumber = 1;

    public event Action OnWaveCompleted;

    /// <summary>
    /// Set the powerup prefabs that can drop from enemies
    /// </summary>
    public void SetPowerupPrefabs(GameObject[] prefabs)
    {
        powerupPrefabs = prefabs;
    }

    public void StartWave(WaveConfig config, int waveNumber = 1)
    {
        if (config == null)
        {
            Debug.LogError("EnemySpawner: WaveConfig is null.");
            return;
        }

        currentWave = config;
        currentWaveNumber = waveNumber;
        IsWaveComplete = false;
        aliveEnemies = 0;
        hasPowerupDroppedThisWave = false;
        
        actualEnemyCount = currentWave.GetRandomizedEnemyCount();
        
        // Infinite mode scaling: more enemies
        if (waveNumber >= infiniteModeStartWave)
        {
            int wavesIntoInfinite = waveNumber - infiniteModeStartWave + 1;
            float countMultiplier = 1f + (wavesIntoInfinite * infiniteCountScalePerWave);
            actualEnemyCount = Mathf.RoundToInt(actualEnemyCount * countMultiplier);
        }

        Debug.Log($"EnemySpawner: Wave {waveNumber} with {actualEnemyCount} enemies.");

        StopAllCoroutines();
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        for (int i = 0; i < actualEnemyCount; i++)
        {
            SpawnEnemy();
            // Get randomized spawn interval for each spawn
            float interval = currentWave.GetRandomizedSpawnInterval();
            yield return new WaitForSeconds(interval);
        }
    }

    private void SpawnEnemy()
    {
        GameObject prefab = currentWave.enemyPrefabs[
            UnityEngine.Random.Range(0, currentWave.enemyPrefabs.Length)
        ];

        Vector2 spawnPos = (Vector2)player.position + UnityEngine.Random.insideUnitCircle.normalized * 10f;

        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
        aliveEnemies++;

        EnemyBase e = enemy.GetComponent<EnemyBase>();
        if (e != null)
        {
            // Apply infinite mode scaling
            if (currentWaveNumber >= infiniteModeStartWave)
            {
                int wavesIntoInfinite = currentWaveNumber - infiniteModeStartWave + 1;
                float hpMultiplier = 1f + (wavesIntoInfinite * infiniteHpScalePerWave);
                float speedMultiplier = 1f + (wavesIntoInfinite * infiniteSpeedScalePerWave);
                
                e.Health *= hpMultiplier;
                e.MaxHealth *= hpMultiplier;
                e.Speed *= speedMultiplier;
            }
            
            e.OnDeath += HandleEnemyDeath;
            
            // Roll for elite status (only after minWaveForElites)
            if (currentWaveNumber >= minWaveForElites)
            {
                float adjustedChance = eliteChance + (currentWaveNumber - minWaveForElites) * 0.005f;
                adjustedChance = Mathf.Min(adjustedChance, 0.15f);
                
                if (UnityEngine.Random.value < adjustedChance)
                {
                    e.MakeElite();
                    e.OnEliteDeath += HandleEliteDeath;
                }
            }
        }
    }

    private void HandleEnemyDeath(EnemyBase enemy)
    {
        enemy.OnDeath -= HandleEnemyDeath;
        enemy.OnEliteDeath -= HandleEliteDeath;
        
        // Try to drop a powerup (max 1 per wave for non-elites)
        if (!enemy.isElite)
        {
            TryDropPowerup(enemy.transform.position);
        }
        
        aliveEnemies--;

        if (aliveEnemies <= 0)
        {
            IsWaveComplete = true;
            OnWaveCompleted?.Invoke();
        }
    }
    
    private void HandleEliteDeath(Vector3 position)
    {
        // Elites ALWAYS drop a powerup (doesn't count toward regular wave drop)
        if (powerupPrefabs == null || powerupPrefabs.Length == 0) return;
        
        GameObject prefab = powerupPrefabs[UnityEngine.Random.Range(0, powerupPrefabs.Length)];
        Instantiate(prefab, position, Quaternion.identity);
        Debug.Log($"Elite powerup dropped at {position}!");
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
}

