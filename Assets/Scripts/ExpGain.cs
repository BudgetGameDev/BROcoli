using UnityEngine;
using Pooling;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ExpGain : MonoBehaviour
{
    public float lifeTime = 30f;
    public int expAmountGain;
    private Rigidbody2D rb;
    private Collider2D col;
    
    // Magnet attraction
    private Transform _playerTransform;
    private PlayerStats _playerStats;
    private const float MagnetSpeed = 12f;  // Speed to move towards player
    private const float MagnetAcceleration = 25f;  // How fast to accelerate
    private float _currentSpeed = 0f;
    
    // Pooling support
    private bool _isPooled = false;
    private float _spawnTime;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col.isTrigger = true;
    }
    
    void OnEnable()
    {
        // Get cached references from GameContext (single lookup, not per-object)
        var context = GameContext.Instance;
        if (context != null)
        {
            _playerTransform = context.PlayerTransform;
            _playerStats = context.PlayerStats;
        }
        
        _spawnTime = Time.time;
        _currentSpeed = 0f;
    }

    public void Init(int expAmount)
    {
        expAmountGain = expAmount;
        _spawnTime = Time.time;
        _currentSpeed = 0f;
        
        // Reset velocity
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        
        // For non-pooled objects, use Destroy with timer
        if (!_isPooled)
        {
            Destroy(gameObject, lifeTime);
        }
    }
    
    /// <summary>
    /// Mark this ExpGain as pooled (affects lifetime handling).
    /// </summary>
    public void SetPooled(bool pooled)
    {
        _isPooled = pooled;
    }
    
    void Update()
    {
        // Check lifetime for pooled objects
        if (_isPooled && Time.time - _spawnTime > lifeTime)
        {
            ReturnToPool();
            return;
        }
        
        // Check for magnet attraction
        if (_playerStats != null && _playerStats.HasMagnetActive && _playerTransform != null)
        {
            float magnetRadius = _playerStats.MagnetRadius;
            float distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
            
            if (distanceToPlayer <= magnetRadius)
            {
                // Accelerate towards player
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, MagnetSpeed, MagnetAcceleration * Time.deltaTime);
                
                Vector2 direction = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
                rb.linearVelocity = direction * _currentSpeed;
            }
        }
        else if (_currentSpeed > 0f)
        {
            // Magnet expired, slow down
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, MagnetAcceleration * Time.deltaTime);
            if (_currentSpeed <= 0.1f)
            {
                rb.linearVelocity = Vector2.zero;
                _currentSpeed = 0f;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Play satisfying pickup sound
            ProceduralXPPickupAudio.PlayPickup();
            
            // Return to pool or destroy
            if (_isPooled)
            {
                ReturnToPool();
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
    
    private void ReturnToPool()
    {
        PoolManager.Instance?.ReturnExpGain(this);
    }
}
