using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveConfig", menuName = "Scriptable Objects/WaveConfig")]
public class WaveConfig : ScriptableObject
{
    [Serializable]
    public class EnemyEntry
    {
        public GameObject prefab;

        [Tooltip("Relative chance vs other entries. 1 = normal, 2 = twice as likely.")]
        public int weight = 1;

        [Tooltip("Only spawn after this many seconds INTO the wave.")]
        public float startAfterSeconds = 0f;

        [Tooltip("Stop spawning after this many seconds INTO the wave (0 = never stop).")]
        public float endAfterSeconds = 0f;
    }

    [Header("Enemies in this wave")]
    public EnemyEntry[] enemies;

    [Header("Optional: override spawner difficulty for this wave")]
    public bool overrideSpawnerValues = false;
    public float baseSpawnInterval = 1f;
    public float intervalMultiplier = 0.85f;
    public float stepSeconds = 5f;
    public float minSpawnInterval = 0.1f;
}