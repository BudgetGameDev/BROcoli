using UnityEngine;

/// <summary>
/// Singleton that caches frequently-accessed global references.
/// Eliminates expensive FindFirstObjectByType/FindGameObjectWithTag calls
/// that were running in every enemy's Awake/Start.
/// </summary>
public class GameContext : MonoBehaviour
{
    private static GameContext _instance;
    private static bool _isQuitting;

    /// <summary>
    /// Singleton instance. Auto-creates if not present in scene.
    /// </summary>
    public static GameContext Instance
    {
        get
        {
            if (_isQuitting) return null;
            
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GameContext>();
                if (_instance == null)
                {
                    var go = new GameObject("[GameContext]");
                    _instance = go.AddComponent<GameContext>();
                }
            }
            return _instance;
        }
    }

    // Cached references
    private Transform _playerTransform;
    private PlayerStats _playerStats;
    private PlayerController _playerController;
    private GameStates _gameStates;
    private bool _initialized;

    /// <summary>
    /// Cached player transform. Returns null if player not found.
    /// </summary>
    public Transform PlayerTransform
    {
        get
        {
            EnsureInitialized();
            return _playerTransform;
        }
    }

    /// <summary>
    /// Cached PlayerStats component. Returns null if not found.
    /// </summary>
    public PlayerStats PlayerStats
    {
        get
        {
            EnsureInitialized();
            return _playerStats;
        }
    }

    /// <summary>
    /// Cached PlayerController component. Returns null if not found.
    /// </summary>
    public PlayerController PlayerController
    {
        get
        {
            EnsureInitialized();
            return _playerController;
        }
    }

    /// <summary>
    /// Cached GameStates instance. Returns null if not found.
    /// </summary>
    public GameStates GameStates
    {
        get
        {
            EnsureInitialized();
            return _gameStates;
        }
    }

    /// <summary>
    /// Whether all references were successfully found.
    /// </summary>
    public bool IsValid => _playerTransform != null && _gameStates != null;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        Initialize();
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }

    private void Initialize()
    {
        // Find player - single lookup for all components
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
            _playerStats = playerObj.GetComponent<PlayerStats>();
            _playerController = playerObj.GetComponent<PlayerController>();
        }

        // Find game states
        _gameStates = FindFirstObjectByType<GameStates>();

        _initialized = true;

        if (_playerTransform == null)
            Debug.LogWarning("[GameContext] Player not found in scene");
        if (_gameStates == null)
            Debug.LogWarning("[GameContext] GameStates not found in scene");
    }

    /// <summary>
    /// Force re-initialization of cached references.
    /// Call this if player or game state objects are recreated.
    /// </summary>
    public void Refresh()
    {
        _initialized = false;
        Initialize();
    }

    /// <summary>
    /// Static helper to quickly check if player exists.
    /// </summary>
    public static bool HasPlayer => Instance != null && Instance.PlayerTransform != null;

    /// <summary>
    /// Reset static state (call on scene unload if needed).
    /// </summary>
    public static void ResetInstance()
    {
        _instance = null;
        _isQuitting = false;
    }
}
