using System;
using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Generic object pool for Unity GameObjects.
    /// Reduces GC pressure by reusing objects instead of Instantiate/Destroy.
    /// </summary>
    public class ObjectPool<T>
        where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _poolParent;
        private readonly Stack<T> _available = new Stack<T>();
        private readonly HashSet<T> _active = new HashSet<T>();
        private readonly Action<T> _onGet;
        private readonly Action<T> _onReturn;
        private readonly int _maxSize;

        /// <summary>
        /// Number of objects currently in use.
        /// </summary>
        public int ActiveCount => _active.Count;

        /// <summary>
        /// Number of objects available in pool.
        /// </summary>
        public int AvailableCount => _available.Count;

        /// <summary>
        /// Total objects created by this pool.
        /// </summary>
        public int TotalCount => _active.Count + _available.Count;

        /// <summary>
        /// Create a new object pool.
        /// </summary>
        /// <param name="prefab">Prefab to instantiate</param>
        /// <param name="initialSize">Number of objects to pre-create</param>
        /// <param name="maxSize">Maximum pool size (0 = unlimited)</param>
        /// <param name="parent">Parent transform for pooled objects</param>
        /// <param name="onGet">Called when object is retrieved from pool</param>
        /// <param name="onReturn">Called when object is returned to pool</param>
        public ObjectPool(
            T prefab,
            int initialSize = 0,
            int maxSize = 0,
            Transform parent = null,
            Action<T> onGet = null,
            Action<T> onReturn = null
        )
        {
            _prefab = prefab;
            _maxSize = maxSize;
            _poolParent = parent;
            _onGet = onGet;
            _onReturn = onReturn;

            PreWarm(initialSize);
        }

        /// <summary>
        /// Pre-create objects to avoid runtime instantiation hitches.
        /// </summary>
        public void PreWarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_maxSize > 0 && TotalCount >= _maxSize)
                    break;

                T obj = CreateNew();
                obj.gameObject.SetActive(false);
                _available.Push(obj);
            }
        }

        /// <summary>
        /// Get an object from the pool.
        /// </summary>
        public T Get()
        {
            T obj = TakeAvailable();

            if (obj == null)
            {
                if (!CanCreate())
                    return null;

                obj = CreateNew();
            }

            return Activate(obj);
        }

        /// <summary>
        /// Get an object and set its position/rotation.
        /// </summary>
        public T Get(Vector3 position, Quaternion rotation)
        {
            T obj = TakeAvailable();
            if (obj == null)
            {
                if (!CanCreate())
                    return null;

                // Instantiate new objects at their requested pose so their first
                // OnEnable and physics registration never observe the prefab pose.
                obj = CreateNew(position, rotation);
            }
            else
            {
                // Reposition recycled objects while inactive. Enabling a pooled
                // Rigidbody at its previous position can leave that stale pose
                // visible until the next physics sync, causing spawn-time logic
                // such as pickup attraction to latch from the wrong location.
                obj.transform.SetPositionAndRotation(position, rotation);
            }

            return Activate(obj);
        }

        /// <summary>
        /// Return an object to the pool.
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null)
                return;
            if (!_active.Contains(obj))
            {
                Debug.LogWarning(
                    $"[ObjectPool] Trying to return object not from this pool: {obj.name}"
                );
                return;
            }

            _onReturn?.Invoke(obj);
            obj.gameObject.SetActive(false);

            if (_poolParent != null)
            {
                obj.transform.SetParent(_poolParent);
            }

            _active.Remove(obj);
            _available.Push(obj);
        }

        /// <summary>
        /// Return all active objects to the pool.
        /// </summary>
        public void ReturnAll()
        {
            // Copy to avoid modification during iteration
            var activeList = new List<T>(_active);
            foreach (var obj in activeList)
            {
                Return(obj);
            }
        }

        /// <summary>
        /// Destroy all pooled objects.
        /// </summary>
        public void Clear()
        {
            foreach (var obj in _active)
            {
                if (obj != null)
                    UnityEngine.Object.Destroy(obj.gameObject);
            }
            _active.Clear();

            while (_available.Count > 0)
            {
                var obj = _available.Pop();
                if (obj != null)
                    UnityEngine.Object.Destroy(obj.gameObject);
            }
        }

        private T TakeAvailable()
        {
            T obj = null;
            while (_available.Count > 0 && obj == null)
                obj = _available.Pop();

            return obj;
        }

        private bool CanCreate()
        {
            if (_maxSize <= 0 || _active.Count < _maxSize)
                return true;

            Debug.LogWarning($"[ObjectPool] Pool for {_prefab.name} at max capacity ({_maxSize})");
            return false;
        }

        private T Activate(T obj)
        {
            obj.gameObject.SetActive(true);
            _active.Add(obj);
            _onGet?.Invoke(obj);
            return obj;
        }

        private T CreateNew()
        {
            T obj = UnityEngine.Object.Instantiate(_prefab, _poolParent);
            obj.name = $"{_prefab.name} (Pooled)";
            return obj;
        }

        private T CreateNew(Vector3 position, Quaternion rotation)
        {
            T obj = UnityEngine.Object.Instantiate(_prefab, position, rotation, _poolParent);
            obj.name = $"{_prefab.name} (Pooled)";
            return obj;
        }
    }
}
