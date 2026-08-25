using System;
using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public Transform player;

    public bool IsWaveComplete { get; private set; }

    [Header("Powerup Drops")]
    [SerializeField] private GameObject[] powerupPrefabs;
    [SerializeField, Range(0f, 1f)] private float powerupDropChance = 0.04f;
    [SerializeField, Min(0f)] private float minimumPowerupDropInterval = 15f;
    [SerializeField, Min(1f)] private float fallbackPickupSpacing = 12f;
    
    private int aliveEnemies;
    private WaveConfig currentWave;
    private float nextPowerupDropTime;

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

        float spawnDistance = Mathf.Max(4f, currentWave.spawnDistance);
        Vector2 spawnDirection = UnityEngine.Random.insideUnitCircle.normalized;
        if (spawnDirection == Vector2.zero) spawnDirection = Vector2.up;
        Vector2 spawnPos = (Vector2)player.position + spawnDirection * spawnDistance;

        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
        TrackEnemy(enemy.GetComponent<EnemyBase>());
    }

    private void TrackEnemy(EnemyBase enemy)
    {
        if (enemy == null) return;

        aliveEnemies++;
        enemy.OnDeath += HandleEnemyDeath;

        if (enemy is HydraEnemyScript hydra)
        {
            hydra.OnChildSpawned += HandleHydraChildSpawned;
        }
    }

    private void HandleHydraChildSpawned(HydraEnemyScript child)
    {
        TrackEnemy(child);
    }

    private void HandleEnemyDeath(EnemyBase enemy)
    {
        enemy.OnDeath -= HandleEnemyDeath;

        if (enemy is HydraEnemyScript hydra)
        {
            hydra.OnChildSpawned -= HandleHydraChildSpawned;
        }
        
        // Try a weighted, cooldown- and spacing-limited powerup drop.
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
        if (powerupPrefabs == null || powerupPrefabs.Length == 0) return;
        if (Time.time < nextPowerupDropTime) return;
        if (UnityEngine.Random.value > powerupDropChance) return;

        // Never allow pickups to cluster within roughly one visible screen.
        if (BoostBase.IsScreenAreaOccupied(position, Camera.main, fallbackPickupSpacing)) return;

        GameObject prefab = ChooseWeightedPowerup();
        if (prefab == null) return;

        Instantiate(prefab, position, Quaternion.identity);
        nextPowerupDropTime = Time.time + minimumPowerupDropInterval;
        
        Debug.Log($"Dropped {prefab.name} at {position}.");
    }

    private GameObject ChooseWeightedPowerup()
    {
        float totalWeight = 0f;

        foreach (GameObject prefab in powerupPrefabs)
        {
            BoostBase boost = prefab != null ? prefab.GetComponent<BoostBase>() : null;
            if (boost != null) totalWeight += Mathf.Max(0f, boost.DropWeight);
        }

        if (totalWeight <= 0f) return null;

        float roll = UnityEngine.Random.value * totalWeight;
        foreach (GameObject prefab in powerupPrefabs)
        {
            BoostBase boost = prefab != null ? prefab.GetComponent<BoostBase>() : null;
            if (boost == null) continue;

            roll -= Mathf.Max(0f, boost.DropWeight);
            if (roll <= 0f) return prefab;
        }

        return powerupPrefabs[powerupPrefabs.Length - 1];
    }
}
