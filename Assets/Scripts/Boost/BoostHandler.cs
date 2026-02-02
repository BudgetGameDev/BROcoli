using UnityEngine;

/// <summary>
/// Provides powerup/boost prefabs for enemy drop system.
/// Timer-based spawning is disabled - powerups now drop from enemies (max 1 per wave).
/// Assign powerupPrefabs in WaveGenerator instead for the new drop system.
/// </summary>
public class BoostHandler : MonoBehaviour
{
    [SerializeField] GameStates _gameState;
    [SerializeField] private Transform _player;
    [SerializeField] private GameObject[] _boosters;
    [SerializeField] private float _spawnDistance = 2f;
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Transform _spawnerParent;

    /// <summary>
    /// Get the array of boost prefabs for use by other systems (e.g., enemy drops)
    /// </summary>
    public GameObject[] BoostPrefabs => _boosters;

    // Timer-based spawning disabled - powerups now drop from enemies via EnemySpawner
    // To re-enable, uncomment the Update method below

    /*
    [SerializeField] private float _spawnRateInterval = 90f;
    private float _nextSpawnTime = 0f;

    private void Awake()
    {
        _nextSpawnTime = Time.time + _spawnRateInterval;
    }

    private void Update()
    {
        if (Time.time >= _nextSpawnTime)
        {
            SpawnBooster();
            _nextSpawnTime = Time.time + _spawnRateInterval;
        }
    }
    */

    /// <summary>
    /// Spawn a random booster at a specific position (for external use)
    /// </summary>
    public void SpawnBoosterAt(Vector2 position)
    {
        if (_boosters.Length == 0) return;

        GameObject prefab = _boosters[Random.Range(0, _boosters.Length)];
        Instantiate(
            prefab, 
            position, 
            Quaternion.identity, 
            _spawnerParent != null ? _spawnerParent : transform
        );
    }

    private Vector2 GetOffscreenPosition()
    {
        float camHeight = _mainCamera.orthographicSize;
        float camWidth = camHeight * _mainCamera.aspect;

        Vector2 playerPos = _player.position;

        int side = Random.Range(0, 4);

        return side switch
        {
            // left
            0 => new Vector2(
                playerPos.x - camWidth - _spawnDistance,
                playerPos.y + Random.Range(-camHeight, camHeight)
            ),
            // right
            1 => new Vector2(
                playerPos.x + camWidth + _spawnDistance,
                playerPos.y + Random.Range(-camHeight, camHeight)
            ),
            // top
            2 => new Vector2(
                playerPos.x + UnityEngine.Random.Range(-camWidth, camWidth),
                playerPos.y + camHeight + _spawnDistance
            ),
            // bottom
            _ => new Vector2(
                playerPos.x + UnityEngine.Random.Range(-camWidth, camWidth),
                playerPos.y - camHeight - _spawnDistance
            ),
        };
    }
}
