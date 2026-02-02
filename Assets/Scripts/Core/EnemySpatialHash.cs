using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spatial hash grid for efficient enemy neighbor queries.
/// Replaces O(N²) Physics2D.OverlapCircleAll calls with O(N) grid lookups.
/// </summary>
public class EnemySpatialHash : MonoBehaviour
{
    private static EnemySpatialHash _instance;
    private static bool _applicationIsQuitting = false;
    
    public static EnemySpatialHash Instance
    {
        get
        {
            // Don't create new instance during application quit / scene teardown
            if (_applicationIsQuitting)
            {
                return null;
            }
            
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<EnemySpatialHash>();
                if (_instance == null && !_applicationIsQuitting)
                {
                    var go = new GameObject("[EnemySpatialHash]");
                    _instance = go.AddComponent<EnemySpatialHash>();
                }
            }
            return _instance;
        }
    }

    // Cell size should be approximately 2x the separation radius for optimal performance
    // Default separation radius is 2.5f, so cell size of 5f works well
    private const float CellSize = 5f;
    private const float InverseCellSize = 1f / CellSize;

    // Grid storage: key is cell hash, value is list of enemies in that cell
    private Dictionary<long, List<EnemyBase>> _grid = new Dictionary<long, List<EnemyBase>>();
    
    // Track which cell each enemy is in for efficient updates
    private Dictionary<EnemyBase, long> _enemyCells = new Dictionary<EnemyBase, long>();
    
    // Reusable list for query results to avoid allocations
    private List<EnemyBase> _queryResults = new List<EnemyBase>(32);
    
    // Pool of lists to avoid allocations
    private Stack<List<EnemyBase>> _listPool = new Stack<List<EnemyBase>>();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// Get cell hash from world position.
    /// </summary>
    private long GetCellHash(Vector2 position)
    {
        int x = Mathf.FloorToInt(position.x * InverseCellSize);
        int y = Mathf.FloorToInt(position.y * InverseCellSize);
        // Combine x and y into a single long hash
        return ((long)x << 32) | (uint)y;
    }

    /// <summary>
    /// Register an enemy with the spatial hash.
    /// Call this in enemy's OnEnable.
    /// </summary>
    public void Register(EnemyBase enemy)
    {
        if (enemy == null || _enemyCells.ContainsKey(enemy)) return;

        Vector2 pos = enemy.transform.position;
        long cellHash = GetCellHash(pos);

        if (!_grid.TryGetValue(cellHash, out var cellList))
        {
            cellList = GetListFromPool();
            _grid[cellHash] = cellList;
        }

        cellList.Add(enemy);
        _enemyCells[enemy] = cellHash;
    }

    /// <summary>
    /// Unregister an enemy from the spatial hash.
    /// Call this in enemy's OnDisable.
    /// </summary>
    public void Unregister(EnemyBase enemy)
    {
        if (enemy == null || !_enemyCells.TryGetValue(enemy, out long cellHash)) return;

        if (_grid.TryGetValue(cellHash, out var cellList))
        {
            cellList.Remove(enemy);
            if (cellList.Count == 0)
            {
                _grid.Remove(cellHash);
                ReturnListToPool(cellList);
            }
        }

        _enemyCells.Remove(enemy);
    }

    /// <summary>
    /// Update an enemy's position in the grid.
    /// Call this in enemy's FixedUpdate before separation.
    /// </summary>
    public void UpdatePosition(EnemyBase enemy)
    {
        if (enemy == null || !_enemyCells.TryGetValue(enemy, out long oldHash)) return;

        Vector2 pos = enemy.transform.position;
        long newHash = GetCellHash(pos);

        // If still in same cell, no update needed
        if (oldHash == newHash) return;

        // Remove from old cell
        if (_grid.TryGetValue(oldHash, out var oldList))
        {
            oldList.Remove(enemy);
            if (oldList.Count == 0)
            {
                _grid.Remove(oldHash);
                ReturnListToPool(oldList);
            }
        }

        // Add to new cell
        if (!_grid.TryGetValue(newHash, out var newList))
        {
            newList = GetListFromPool();
            _grid[newHash] = newList;
        }
        newList.Add(enemy);
        _enemyCells[enemy] = newHash;
    }

    /// <summary>
    /// Get all enemies within radius of position.
    /// Returns a reusable list - do not cache the result!
    /// </summary>
    public List<EnemyBase> GetNearbyEnemies(Vector2 position, float radius)
    {
        _queryResults.Clear();

        // Calculate which cells to check (all cells that could contain enemies within radius)
        int minX = Mathf.FloorToInt((position.x - radius) * InverseCellSize);
        int maxX = Mathf.FloorToInt((position.x + radius) * InverseCellSize);
        int minY = Mathf.FloorToInt((position.y - radius) * InverseCellSize);
        int maxY = Mathf.FloorToInt((position.y + radius) * InverseCellSize);

        float radiusSqr = radius * radius;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                long cellHash = ((long)x << 32) | (uint)y;
                if (_grid.TryGetValue(cellHash, out var cellList))
                {
                    for (int i = 0; i < cellList.Count; i++)
                    {
                        EnemyBase enemy = cellList[i];
                        if (enemy == null) continue;

                        float distSqr = ((Vector2)enemy.transform.position - position).sqrMagnitude;
                        if (distSqr <= radiusSqr)
                        {
                            _queryResults.Add(enemy);
                        }
                    }
                }
            }
        }

        return _queryResults;
    }

    /// <summary>
    /// Get count of registered enemies.
    /// </summary>
    public int EnemyCount => _enemyCells.Count;

    /// <summary>
    /// Clear all data. Call on scene unload.
    /// </summary>
    public void Clear()
    {
        foreach (var list in _grid.Values)
        {
            ReturnListToPool(list);
        }
        _grid.Clear();
        _enemyCells.Clear();
    }

    private List<EnemyBase> GetListFromPool()
    {
        return _listPool.Count > 0 ? _listPool.Pop() : new List<EnemyBase>(8);
    }

    private void ReturnListToPool(List<EnemyBase> list)
    {
        list.Clear();
        _listPool.Push(list);
    }

    /// <summary>
    /// Reset static instance (call on scene unload if needed).
    /// </summary>
    public static void ResetInstance()
    {
        if (_instance != null)
        {
            _instance.Clear();
        }
        _instance = null;
    }
}
