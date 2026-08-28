using Pooling;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ExpGain : MonoBehaviour
{
    public float lifeTime = 30f;
    public int expAmountGain;
    private Rigidbody rb;
    private Collider col;
    private PickupVisual3D _pickupVisual;
    private float _currentSpeed = 0f;
    private bool _localAttractionLocked;
    private bool _isCollected;

    // Pooling support
    private bool _isPooled = false;
    private float _spawnTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        _pickupVisual = PickupVisual3D.AttachExperience(gameObject);
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        col.isTrigger = true;
    }

    void OnEnable()
    {
        _isCollected = false;
        _spawnTime = Time.time;
        PickupAttraction.Reset(rb, ref _currentSpeed, ref _localAttractionLocked, _pickupVisual);
    }

    public void Init(int expAmount)
    {
        expAmountGain = expAmount;
        _spawnTime = Time.time;
        PickupAttraction.Reset(rb, ref _currentSpeed, ref _localAttractionLocked, _pickupVisual);

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
            _pickupVisual
        );
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isCollected)
            return;

        PlayerStats stats = other.GetComponentInParent<PlayerStats>();
        if (stats == null)
            return;

        _isCollected = true;
        int experience = expAmountGain;

        // Hide/recycle before applying XP. Applying XP can pause the game for
        // a level-up choice, so the orb must already be gone at that point.
        if (_isPooled)
            ReturnToPool();
        else
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        ProceduralXPPickupAudio.PlayPickup();
        stats.ApplyExperience(experience);
    }

    private void ReturnToPool()
    {
        PoolManager.Instance?.ReturnExpGain(this);
    }
}
