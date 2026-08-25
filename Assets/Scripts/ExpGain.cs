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
    
    // Global magnet attraction
    private const float MinimumMagnetSpeed = 18f;
    private const float MaximumMagnetSpeed = 60f;
    private const float MagnetAcceleration = 120f;
    private float _currentSpeed = 0f;
    
    // Pooling support
    private bool _isPooled = false;
    private float _spawnTime;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col.isTrigger = true;
    }
    
    void OnEnable()
    {
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
        
    }

    void FixedUpdate()
    {
        Transform target = PlayerStats.ActiveMagnetTarget;
        if (target != null && rb != null)
        {
            Vector2 toPlayer = (Vector2)target.position - rb.position;
            float distance = toPlayer.magnitude;
            if (distance <= 0.001f)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            float targetSpeed = Mathf.Clamp(
                distance * 4f,
                MinimumMagnetSpeed,
                MaximumMagnetSpeed);
            _currentSpeed = Mathf.MoveTowards(
                _currentSpeed,
                targetSpeed,
                MagnetAcceleration * Time.fixedDeltaTime);
            Vector2 direction = toPlayer / distance;
            float arrivalSpeed = distance * 0.75f / Mathf.Max(Time.fixedDeltaTime, 0.001f);
            rb.WakeUp();
            rb.linearVelocity = direction * Mathf.Min(_currentSpeed, arrivalSpeed);
        }
        else if (_currentSpeed > 0f && rb != null)
        {
            _currentSpeed = Mathf.MoveTowards(
                _currentSpeed,
                0f,
                MagnetAcceleration * Time.fixedDeltaTime);
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
