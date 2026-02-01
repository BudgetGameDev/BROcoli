using UnityEngine;

[CreateAssetMenu(menuName = "Waves/Wave Config")]
public class WaveConfig : ScriptableObject
{
    public GameObject[] enemyPrefabs;
    public int enemyCount = 10;
    public float spawnInterval = 0.5f;
}
