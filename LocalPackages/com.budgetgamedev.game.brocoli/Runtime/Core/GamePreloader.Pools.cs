using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class GamePreloader
    {
        /// <summary>
        /// Load the pooled prefabs from Resources and record the ones the pools need.
        /// Returns every prefab that also wants an offscreen instantiate warmup.
        /// </summary>
        private List<GameObject> CollectPoolPrefabs()
        {
            _enemyPrefabs.Clear();
            _projectilePrefabs.Clear();
            _expGainPrefab = null;

            List<GameObject> prefabsToWarm = new List<GameObject>();

            // Collect enemy prefabs
            foreach (GameObject prefab in Resources.LoadAll<GameObject>(EnemyPrefabPath))
            {
                if (prefab.GetComponent<EnemyBase>() != null)
                {
                    prefabsToWarm.Add(prefab);
                    _enemyPrefabs.Add(prefab); // Store for pooling
                }
            }

            // Collect boost/projectile prefabs
            foreach (GameObject prefab in Resources.LoadAll<GameObject>(BoostPrefabPath))
            {
                if (
                    prefab.name.StartsWith("Boost")
                    || prefab.name.Contains("Projectile")
                    || prefab.name.Contains("FireBall")
                    || prefab.name.Contains("Exp")
                )
                {
                    prefabsToWarm.Add(prefab);

                    // Store projectile prefabs for pooling
                    if (prefab.GetComponent<EnemyProjectile>() != null)
                    {
                        _projectilePrefabs.Add(prefab);
                    }

                    // Store ExpGain prefab for pooling
                    var expGain = prefab.GetComponent<ExpGain>();
                    if (expGain != null && _expGainPrefab == null)
                    {
                        _expGainPrefab = expGain;
                    }
                }
            }

            if (logPreloadSteps)
                Debug.Log(
                    $"[GamePreloader] Found {prefabsToWarm.Count} prefabs ({_enemyPrefabs.Count} enemies, {_projectilePrefabs.Count} projectiles)"
                );

            return prefabsToWarm;
        }

        private void WarmupPools()
        {
            // Initialize GameContext singleton early
            var context = GameContext.Instance;

            // Initialize EnemySpatialHash singleton early
            var spatialHash = EnemySpatialHash.Instance;

            // Pre-warm object pools
            PoolManager.Instance.PreWarmAll(
                _enemyPrefabs.ToArray(),
                _expGainPrefab,
                _projectilePrefabs.ToArray()
            );

            if (logPreloadSteps)
                Debug.Log("[GamePreloader] Object pools pre-warmed");
        }
    }
}
