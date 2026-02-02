using System;
using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public Transform player;

    public bool IsWaveComplete { get; private set; }

    [Header("Powerup Drops")]
    [SerializeField] private GameObject[] powerupPrefabs;
    [SerializeField] private float powerupDropChance = 0.15f; // 15% chance per enemy kill
    
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
        hasPowerupDroppedThisWave = false; // Reset powerup drop for new wave

        Debug.Log($"EnemySpawner: Starting wave with {currentWave.enemyCount} enemies.");

        StopAllCoroutines();
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        for (int i = 0; i < currentWave.enemyCount; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(currentWave.spawnInterval);
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
            e.OnDeath += HandleEnemyDeath;
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
}

