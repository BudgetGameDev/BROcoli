using System.Linq;
using UnityEngine;

public class EnemySpawnerOld : MonoBehaviour
{
    [Header("Spawn Rate Curve (percentage-based)")]
    public float baseSpawnInterval = 1f;        // starting interval (seconds)
    public float intervalMultiplier = 0.85f;    // 15% harder => interval * 0.85
    public float stepSeconds = 5f;              // apply the multiplier every 5 seconds
    public float minSpawnInterval = 0.1f;       // clamp so it doesn't go insane

    public float currentInterval;

    [SerializeField] GameStates gameStates;
    private float nextSpawnTime;

    public GameObject[] enemyPrefabs;

    [Header("Spawn Settings")]
    public Transform player;
    public float spawnDistance = 10f;

    void Start()
    {
        gameStates = FindFirstObjectByType<GameStates>();
        nextSpawnTime = 0f;
    }

    void Update()
    {
        float curTime = gameStates.gameTime;
        float currentInterval = GetCurrentInterval(curTime);
        this.currentInterval = currentInterval;

        if (curTime >= nextSpawnTime)
        {
            SpawnEnemy();
            nextSpawnTime = curTime + currentInterval;
        }
    }

    float GetCurrentInterval(float elapsed)
    {
        // exponential curve: interval = base * multiplier^(elapsed/stepSeconds)
        float scaled = baseSpawnInterval * Mathf.Pow(intervalMultiplier, elapsed / stepSeconds);
        return Mathf.Max(minSpawnInterval, scaled);
    }
        
    void SpawnEnemy()
    {
        // get the list of enemy prefabs ready to spawn aka whatTimeToStartSpawning = < elapsedTime
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;
        if (player == null) return;

        GameObject[] enemyPrefabsReady = enemyPrefabs.Where(ep =>
        {
            EnemyBase es = ep.GetComponent<EnemyBase>();
            return es != null && es.TimeToStartSpawning <= gameStates.gameTime && es.TimeToEndSpawning >= gameStates.gameTime;
        }).ToArray();
        
        if (enemyPrefabsReady == null || enemyPrefabsReady.Length == 0) return;
        
        Vector2 spawnPosition = GetPositionAroundPlayer();

        GameObject prefab =
            enemyPrefabsReady[Random.Range(0, enemyPrefabsReady.Length)];

        Instantiate(prefab, spawnPosition, Quaternion.identity);
    }

    Vector2 GetPositionAroundPlayer()
    {
        // Random direction on a circle
        Vector2 direction = Random.insideUnitCircle.normalized;

        return (Vector2)player.position + direction * spawnDistance;
    }
}