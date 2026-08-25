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
    private PickupVisual3D _pickupVisual;
    private float _currentSpeed = 0f;
    private bool _localAttractionLocked;
    
    // Pooling support
    private bool _isPooled = false;
    private float _spawnTime;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        _pickupVisual = PickupVisual3D.AttachExperience(gameObject);
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col.isTrigger = true;
    }
    
    void OnEnable()
    {
        _spawnTime = Time.time;
        PickupAttraction.Reset(
            rb,
            ref _currentSpeed,
            ref _localAttractionLocked,
            _pickupVisual);
    }

    public void Init(int expAmount)
    {
        expAmountGain = expAmount;
        _spawnTime = Time.time;
        PickupAttraction.Reset(
            rb,
            ref _currentSpeed,
            ref _localAttractionLocked,
            _pickupVisual);
        
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
        PickupAttraction.UpdateMotion(
            rb,
            ref _currentSpeed,
            ref _localAttractionLocked,
            _pickupVisual);
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
