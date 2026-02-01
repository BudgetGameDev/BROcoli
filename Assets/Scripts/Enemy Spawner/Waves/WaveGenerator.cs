using System.Collections;
using UnityEngine;

public class WaveGenerator : MonoBehaviour
{
    private static WaitForSeconds _waitForSeconds1 = new(1f);

    [SerializeField] private float preWaveCountdownSeconds = 5f;
    [SerializeField] private EnemySpawner enemySpawnerPrefab;
    [SerializeField] private Transform spawnerParent;
    [SerializeField] private Transform player;
    [SerializeField] private GameStates gameStates;
    [SerializeField] private WaveConfig[] waves;

    private EnemySpawner spawner;
    private int currentWave = 1;

    private void Start()
    {
        spawner = Instantiate(
            enemySpawnerPrefab,
            spawnerParent != null ? spawnerParent : transform
        );
        spawner.player = player;

        StartCoroutine(WaveLoop());
    }

    private IEnumerator WaveLoop()
    {
        while (gameStates == null || gameStates.IsGameOver == false)
        {
            yield return StartCoroutine(PreWaveCountdown());

            WaveConfig config = GetWaveConfig(currentWave);

            Debug.Log($"Starting Wave {currentWave}...");

            spawner.StartWave(config);

            yield return new WaitUntil(() => spawner.IsWaveComplete);

            currentWave++;
        }
    }

    private IEnumerator PreWaveCountdown()
    {
        for (int i = Mathf.CeilToInt(preWaveCountdownSeconds); i > 0; i--)
        {
            Debug.Log($"Wave {currentWave} starts in {i}...");
            yield return _waitForSeconds1;
        }
    }

    private WaveConfig GetWaveConfig(int waveNumber)
    {
        if (waves == null || waves.Length == 0)
            return null;

        int index = Mathf.Clamp(waveNumber - 1, 0, waves.Length - 1);
        return waves[index];
    }
}
