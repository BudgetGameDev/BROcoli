using System.Collections.Generic;
using UnityEngine;

namespace Pooling
{
    /// <summary>
    /// Central registry for all object pools. Handles pre-warming during loading.
    /// </summary>
    public class PoolManager : MonoBehaviour
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
        private const int EnemiesPerType = 20;
        private const int ProjectileCount = 50;
        private const int ExpGainCount = 100;

        // Enemy pools keyed by prefab instance ID
        private Dictionary<int, ObjectPool<EnemyBase>> _enemyPools = new Dictionary<int, ObjectPool<EnemyBase>>();
        
        // Projectile pools
        private Dictionary<int, ObjectPool<EnemyProjectile>> _projectilePools = new Dictionary<int, ObjectPool<EnemyProjectile>>();
        
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
        public void PreWarmAll(GameObject[] enemyPrefabs, ExpGain expGainPrefab, GameObject[] projectilePrefabs = null)
        {
            if (_isPrewarmed) return;

            // Pre-warm enemy pools
            if (enemyPrefabs != null)
            {
                foreach (var prefab in enemyPrefabs)
                {
                    if (prefab == null) continue;
                    var enemy = prefab.GetComponent<EnemyBase>();
                    if (enemy != null)
                    {
                        GetOrCreateEnemyPool(enemy).PreWarm(EnemiesPerType);
                    }
                }
            }

            // Pre-warm ExpGain pool
            if (expGainPrefab != null)
            {
                _expGainPrefab = expGainPrefab;
                _expGainPool = new ObjectPool<ExpGain>(
                    expGainPrefab,
                    ExpGainCount,
                    ExpGainCount * 2,
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
                    if (prefab == null) continue;
                    var proj = prefab.GetComponent<EnemyProjectile>();
                    if (proj != null)
                    {
                        GetOrCreateProjectilePool(proj).PreWarm(ProjectileCount);
                    }
                }
            }

            _isPrewarmed = true;
            Debug.Log($"[PoolManager] Pre-warmed pools: {_enemyPools.Count} enemy types, " +
                      $"{_projectilePools.Count} projectile types, ExpGain pool");
        }

        #region Enemy Pool

        /// <summary>
        /// Get an enemy from the pool.
        /// </summary>
        public EnemyBase GetEnemy(EnemyBase prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            var pool = GetOrCreateEnemyPool(prefab);
            return pool.Get(position, rotation);
        }

        /// <summary>
        /// Return an enemy to the pool.
        /// </summary>
        public void ReturnEnemy(EnemyBase enemy)
        {
            if (enemy == null) return;

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
            int id = prefab.gameObject.GetInstanceID();
            if (!_enemyPools.TryGetValue(id, out var pool))
            {
                pool = new ObjectPool<EnemyBase>(
                    prefab,
                    0, // Don't pre-warm here, do it in PreWarmAll
                    EnemiesPerType * 3, // Allow growth but cap it
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
                rb.simulated = true;
                rb.linearVelocity = Vector2.zero;
            }

            foreach (var col in enemy.GetComponents<Collider2D>())
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
                rb.simulated = false;
            }

            foreach (var col in enemy.GetComponents<Collider2D>())
            {
                col.enabled = false;
            }
        }

        #endregion

        #region ExpGain Pool

        /// <summary>
        /// Get an ExpGain from the pool.
        /// </summary>
        public ExpGain GetExpGain(Vector3 position)
        {
            if (_expGainPool == null)
            {
                Debug.LogWarning("[PoolManager] ExpGain pool not initialized");
                return null;
            }
            return _expGainPool.Get(position, Quaternion.identity);
        }

        /// <summary>
        /// Return an ExpGain to the pool.
        /// </summary>
        public void ReturnExpGain(ExpGain expGain)
        {
            if (_expGainPool == null || expGain == null)
            {
                if (expGain != null) Destroy(expGain.gameObject);
                return;
            }
            _expGainPool.Return(expGain);
        }

        private void OnExpGainGet(ExpGain exp)
        {
            // Reset state
            var rb = exp.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = true;
                rb.linearVelocity = Vector2.zero;
            }

            var col = exp.GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = true;
            }
        }

        private void OnExpGainReturn(ExpGain exp)
        {
            var rb = exp.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = false;
            }

            var col = exp.GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = false;
            }
        }

        #endregion

        #region Projectile Pool

        /// <summary>
        /// Get a projectile from the pool.
        /// </summary>
        public EnemyProjectile GetProjectile(EnemyProjectile prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            var pool = GetOrCreateProjectilePool(prefab);
            return pool.Get(position, rotation);
        }

        /// <summary>
        /// Return a projectile to the pool.
        /// </summary>
        public void ReturnProjectile(EnemyProjectile projectile)
        {
            if (projectile == null) return;

            int prefabId = GetPrefabId(projectile);
            if (_projectilePools.TryGetValue(prefabId, out var pool))
            {
                pool.Return(projectile);
            }
            else
            {
                Destroy(projectile.gameObject);
            }
        }

        private ObjectPool<EnemyProjectile> GetOrCreateProjectilePool(EnemyProjectile prefab)
        {
            int id = prefab.gameObject.GetInstanceID();
            if (!_projectilePools.TryGetValue(id, out var pool))
            {
                pool = new ObjectPool<EnemyProjectile>(
                    prefab,
                    0,
                    ProjectileCount * 2,
                    _poolContainer,
                    OnProjectileGet,
                    OnProjectileReturn
                );
                _projectilePools[id] = pool;
            }
            return pool;
        }

        private void OnProjectileGet(EnemyProjectile proj)
        {
            var rb = proj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = true;
                rb.linearVelocity = Vector2.zero;
            }

            foreach (var col in proj.GetComponents<Collider2D>())
            {
                col.enabled = true;
            }
        }

        private void OnProjectileReturn(EnemyProjectile proj)
        {
            var rb = proj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = false;
            }

            foreach (var col in proj.GetComponents<Collider2D>())
            {
                col.enabled = false;
            }
        }

        #endregion

        /// <summary>
        /// Get a stable ID for pooled objects based on their original prefab.
        /// </summary>
        private int GetPrefabId(Component obj)
        {
            // Use the name to identify the prefab type (strips " (Pooled)" suffix)
            string name = obj.gameObject.name.Replace(" (Pooled)", "");
            return name.GetHashCode();
        }

        /// <summary>
        /// Clear all pools.
        /// </summary>
        public void ClearAll()
        {
            foreach (var pool in _enemyPools.Values)
            {
                pool.Clear();
            }
            _enemyPools.Clear();

            foreach (var pool in _projectilePools.Values)
            {
                pool.Clear();
            }
            _projectilePools.Clear();

            _expGainPool?.Clear();
            _expGainPool = null;

            _isPrewarmed = false;
        }

        /// <summary>
        /// Reset static instance.
        /// </summary>
        public static void ResetInstance()
        {
            if (_instance != null)
            {
                _instance.ClearAll();
            }
            _instance = null;
        }
    }
}
