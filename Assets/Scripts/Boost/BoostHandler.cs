using UnityEngine;

public class BoostHandler : MonoBehaviour
{
    [SerializeField] GameStates _gameState;
    [SerializeField] private float _spawnRateInterval = 3f;
    [SerializeField] private Transform _player;
    [SerializeField] private GameObject[] _boosters;
    [SerializeField] private float _spawnDistance = 2f;
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Transform _spawnerParent;

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

    private void SpawnBooster()
    {
        if (_boosters.Length == 0)
        {
            return;
        }

        Vector2 spawnPos = GetOffscreenPosition();
        GameObject prefab = _boosters[Random.Range(0, _boosters.Length)];

        Instantiate(
            prefab, 
            spawnPos, 
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
