using System.Collections.Generic;
using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class PoolManager
    {
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
                if (expGain != null)
                    Destroy(expGain.gameObject);
                return;
            }
            _expGainPool.Return(expGain);
        }

        private void OnExpGainGet(ExpGain exp)
        {
            exp.SetPooled(true);
            var rb = exp.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.SetSimulated(true);
                rb.linearVelocity = Vector3.zero;
            }

            var col = exp.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
            }
        }

        private void OnExpGainReturn(ExpGain exp)
        {
            var rb = exp.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.SetSimulated(false);
            }

            var col = exp.GetComponent<Collider>();
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
        public EnemyProjectile GetProjectile(
            EnemyProjectile prefab,
            Vector3 position,
            Quaternion rotation
        )
        {
            if (prefab == null)
                return null;
            var pool = GetOrCreateProjectilePool(prefab);
            return pool.Get(position, rotation);
        }

        /// <summary>
        /// Return a projectile to the pool.
        /// </summary>
        public void ReturnProjectile(EnemyProjectile projectile)
        {
            if (projectile == null)
                return;

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
            int id = GetPrefabId(prefab);
            if (!_projectilePools.TryGetValue(id, out var pool))
            {
                pool = new ObjectPool<EnemyProjectile>(
                    prefab,
                    0,
                    ProjectilePoolCapacity,
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
            var rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.SetSimulated(true);
                rb.linearVelocity = Vector3.zero;
            }

            foreach (var col in proj.GetComponents<Collider>())
            {
                col.enabled = true;
            }
        }

        private void OnProjectileReturn(EnemyProjectile proj)
        {
            var rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.SetSimulated(false);
            }

            foreach (var col in proj.GetComponents<Collider>())
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
