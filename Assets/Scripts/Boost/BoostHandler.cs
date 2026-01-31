using UnityEngine;

public class BoostHandler : MonoBehaviour
{
    [SerializeField] GameStates _gameState;
    [SerializeField] private float _spawnRateInterval = 3f;
    [SerializeField] private Transform _player;
    [SerializeField] private GameObject[] _boosters;
    [SerializeField] private float _spawnDistance = 2f;
    [SerializeField] private Camera _mainCamera;

    private float _nextSpawnTime = 0f;

    private void Awake()
    {
        _nextSpawnTime = Time.time + _spawnRateInterval;
    }

    private void Update()
    {
        //  TODO: Check if game is over
        //if (_gameState)
        //{

        //}

        if (Time.time >= _nextSpawnTime)
        {
            SpawnBooster();
            _nextSpawnTime = Time.time + _spawnRateInterval;
        }
    }

    private void SpawnBooster()
    {
        if (_boosters.Length == 0)
        {
            return;
        }

        Vector2 spawnPos = GetOffscreenPosition();
        GameObject prefab = _boosters[UnityEngine.Random.Range(0, _boosters.Length)];

        Debug.Log($"Spawning booster {prefab.name} at {spawnPos}");

        Instantiate(prefab, spawnPos, Quaternion.identity);
    }

    private Vector2 GetOffscreenPosition()
    {
        float camHeight = _mainCamera.orthographicSize;
        float camWidth = camHeight * _mainCamera.aspect;

        Vector2 playerPos = _player.position;

        int side = UnityEngine.Random.Range(0, 4);

        return side switch
        {
            // left
            0 => new Vector2(
                playerPos.x - camWidth - _spawnDistance,
                playerPos.y + UnityEngine.Random.Range(-camHeight, camHeight)
            ),
            // right
            1 => new Vector2(
                playerPos.x + camWidth + _spawnDistance,
                playerPos.y + UnityEngine.Random.Range(-camHeight, camHeight)
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
