using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyProjectile : MonoBehaviour
{
    public float damage = 10f;
    public float speed = 10f;
    public float lifeTime = 5f;
    
    [Header("Fizzle Effect")]
    [SerializeField] private float fizzleStartTime = 3f;  // When to start fizzling (seconds before death)
    [SerializeField] private Transform visualTransform;    // The 3D model to scale during fizzle
    [SerializeField] private float spinSpeed = 180f;       // Rotation speed in degrees/second

    private Rigidbody2D rb;
    private Collider2D col;
    private float spawnTime;
    private Vector3 initialScale;
    private bool isFizzling;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col.isTrigger = true;
        
        if (visualTransform != null)
            initialScale = visualTransform.localScale;
    }

    public void Init(Vector2 direction)
    {
        rb.linearVelocity = direction.normalized * speed;
        spawnTime = Time.time;
        
        // Capture initial scale if not already done
        if (visualTransform != null && initialScale == Vector3.zero)
            initialScale = visualTransform.localScale;
            
        Destroy(gameObject, lifeTime);
    }
    
    void Update()
    {
        // Spin the visual
        if (visualTransform != null)
        {
            visualTransform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }
        
        // Fizzle out effect - shrink towards end of life
        float timeAlive = Time.time - spawnTime;
        float fizzleThreshold = lifeTime - fizzleStartTime;
        
        if (timeAlive > fizzleThreshold && visualTransform != null)
        {
            isFizzling = true;
            float fizzleProgress = (timeAlive - fizzleThreshold) / fizzleStartTime;
            float scale = Mathf.Lerp(1f, 0f, fizzleProgress);
            visualTransform.localScale = initialScale * scale;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if hit player
        if (other.CompareTag("Player"))
        {
            PlayerStats stats = other.GetComponentInChildren<PlayerStats>();

            stats.ApplyDamage(damage);
            
            // Play enemy projectile hit sound
            ProceduralEnemyProjectileHitAudio.PlayHit(transform.position, ProceduralEnemyProjectileHitAudio.EnemyHitSoundType.PlasmaImpact, 0.45f);
            
            Destroy(gameObject);
        }
        // Destroy on hitting walls/obstacles (but not other enemies)
        else if (!other.CompareTag("Enemy") && !other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}
