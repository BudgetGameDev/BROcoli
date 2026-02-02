using UnityEngine;

[CreateAssetMenu(menuName = "Waves/Wave Config")]
public class WaveConfig : ScriptableObject
{
    [Header("Enemy Settings")]
    public GameObject[] enemyPrefabs;
    
    [Header("Spawn Count")]
    [Tooltip("Base number of enemies to spawn")]
    public int enemyCount = 10;
    
    [Tooltip("Random variation applied to enemy count (+/- this value). Actual count = enemyCount ± enemyCountVariation")]
    [Min(0)]
    public int enemyCountVariation = 0;
    
    [Header("Spawn Timing")]
    [Tooltip("Base time between enemy spawns in seconds")]
    public float spawnInterval = 0.5f;
    
    [Tooltip("Random variation applied to spawn interval (+/- this value in seconds)")]
    [Min(0f)]
    public float spawnIntervalVariation = 0f;
    
    /// <summary>
    /// Returns a randomized enemy count based on enemyCount and enemyCountVariation.
    /// Result is clamped to a minimum of 1.
    /// </summary>
    public int GetRandomizedEnemyCount()
    {
        if (enemyCountVariation <= 0)
            return enemyCount;
            
        int variation = Random.Range(-enemyCountVariation, enemyCountVariation + 1);
        return Mathf.Max(1, enemyCount + variation);
    }
    
    /// <summary>
    /// Returns a randomized spawn interval based on spawnInterval and spawnIntervalVariation.
    /// Result is clamped to a minimum of 0.1 seconds.
    /// </summary>
    public float GetRandomizedSpawnInterval()
    {
        if (spawnIntervalVariation <= 0f)
            return spawnInterval;
            
        float variation = Random.Range(-spawnIntervalVariation, spawnIntervalVariation);
        return Mathf.Max(0.1f, spawnInterval + variation);
    }
}
