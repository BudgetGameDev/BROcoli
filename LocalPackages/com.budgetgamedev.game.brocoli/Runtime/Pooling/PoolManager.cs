using System.Collections.Generic;
using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Central registry for all object pools. Handles pre-warming during loading.
    /// </summary>
    public partial class PoolManager : MonoBehaviour
    {
        private static PoolManager _instance;
        public static PoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PoolManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[PoolManager]");
                        _instance = go.AddComponent<PoolManager>();
                    }
                }
                return _instance;
            }
        }

        // Pool configuration
        private const int EnemyPrewarmCount = 8;
        private const int EnemyPoolCapacity = 60;
        private const int ProjectilePrewarmCount = 12;
        private const int ProjectilePoolCapacity = 100;
        private const int ExpGainPrewarmCount = 32;
        private const int ExpGainPoolCapacity = 200;

        // Enemy pools keyed by stable prefab type ID so split children reuse their root's pool.
        private Dictionary<int, ObjectPool<EnemyBase>> _enemyPools =
            new Dictionary<int, ObjectPool<EnemyBase>>();

        // Projectile pools
        private Dictionary<int, ObjectPool<EnemyProjectile>> _projectilePools =
            new Dictionary<int, ObjectPool<EnemyProjectile>>();

        // ExpGain pool
        private ObjectPool<ExpGain> _expGainPool;
        private ExpGain _expGainPrefab;

        // Container for pooled objects
        private Transform _poolContainer;

        private bool _isPrewarmed;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Create container for pooled objects (keeps hierarchy clean)
            _poolContainer = new GameObject("PooledObjects").transform;
            _poolContainer.SetParent(transform);
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                ClearAll();
                _instance = null;
            }
        }

        /// <summary>
        /// Pre-warm all pools. Call during loading screen.
        /// </summary>
        /// <param name="enemyPrefabs">Array of enemy prefabs to pool</param>
        /// <param name="expGainPrefab">ExpGain prefab to pool</param>
        /// <param name="projectilePrefabs">Array of projectile prefabs to pool</param>
        public void PreWarmAll(
            GameObject[] enemyPrefabs,
            ExpGain expGainPrefab,
            GameObject[] projectilePrefabs = null
        )
        {
            if (_isPrewarmed)
                return;

            // Pre-warm enemy pools
            if (enemyPrefabs != null)
            {
                foreach (var prefab in enemyPrefabs)
                {
                    if (prefab == null)
                        continue;
                    var enemy = prefab.GetComponent<EnemyBase>();
                    if (enemy != null)
                    {
                        GetOrCreateEnemyPool(enemy).PreWarm(EnemyPrewarmCount);
                    }
                }
            }

            // Pre-warm ExpGain pool
            if (expGainPrefab != null)
            {
                _expGainPrefab = expGainPrefab;
                _expGainPool = new ObjectPool<ExpGain>(
                    expGainPrefab,
                    ExpGainPrewarmCount,
                    ExpGainPoolCapacity,
                    _poolContainer,
                    OnExpGainGet,
                    OnExpGainReturn
                );
            }

            // Pre-warm projectile pools
            if (projectilePrefabs != null)
            {
                foreach (var prefab in projectilePrefabs)
                {
                    if (prefab == null)
                        continue;
                    var proj = prefab.GetComponent<EnemyProjectile>();
                    if (proj != null)
                    {
                        GetOrCreateProjectilePool(proj).PreWarm(ProjectilePrewarmCount);
                    }
                }
            }

            _isPrewarmed = true;
            Debug.Log(
                $"[PoolManager] Pre-warmed pools: {_enemyPools.Count} enemy types, "
                    + $"{_projectilePools.Count} projectile types, ExpGain pool"
            );
        }

        #region Enemy Pool

        /// <summary>
        /// Get an enemy from the pool.
        /// </summary>
        public EnemyBase GetEnemy(EnemyBase prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return null;
            var pool = GetOrCreateEnemyPool(prefab);
            return pool.Get(position, rotation);
        }

        /// <summary>
        /// Return an enemy to the pool.
        /// </summary>
        public void ReturnEnemy(EnemyBase enemy)
        {
            if (enemy == null)
                return;

            // Find the pool this enemy belongs to
            int prefabId = GetPrefabId(enemy);
            if (_enemyPools.TryGetValue(prefabId, out var pool))
            {
                pool.Return(enemy);
            }
            else
            {
                // Fallback: destroy if no pool found
                Destroy(enemy.gameObject);
            }
        }

        private ObjectPool<EnemyBase> GetOrCreateEnemyPool(EnemyBase prefab)
        {
            int id = GetPrefabId(prefab);
            if (!_enemyPools.TryGetValue(id, out var pool))
            {
                pool = new ObjectPool<EnemyBase>(
                    prefab,
                    0, // Don't pre-warm here, do it in PreWarmAll
                    EnemyPoolCapacity,
                    _poolContainer,
                    OnEnemyGet,
                    OnEnemyReturn
                );
                _enemyPools[id] = pool;
            }
            return pool;
        }

        private void OnEnemyGet(EnemyBase enemy)
        {
            // Call ResetForPool to reset all enemy state (health, visuals, attack state, etc.)
            enemy.ResetForPool();

            // Re-enable components
            var rb = enemy.rb;
            if (rb != null)
            {
                rb.SetSimulated(true);
                rb.linearVelocity = Vector3.zero;
            }

            foreach (var col in enemy.GetComponents<Collider>())
            {
                col.enabled = true;
            }

            // Register with spatial hash
            EnemySpatialHash.Instance?.Register(enemy);
        }

        private void OnEnemyReturn(EnemyBase enemy)
        {
            // Unregister from spatial hash
            EnemySpatialHash.Instance?.Unregister(enemy);

            // Disable physics
            var rb = enemy.rb;
            if (rb != null)
            {
                rb.SetSimulated(false);
            }

            foreach (var col in enemy.GetComponents<Collider>())
            {
                col.enabled = false;
            }
        }

        #endregion
    }
}
