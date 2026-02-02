using UnityEngine;

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
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col.isTrigger = true;
        
        // Find player for magnet effect
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _playerStats = player.GetComponent<PlayerStats>();
        }
    }

    public void Init(int expAmount)
    {
        expAmountGain = expAmount;
        Destroy(gameObject, lifeTime);
    }
    
    void Update()
    {
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
            
            Destroy(gameObject);
        }
    }
}
