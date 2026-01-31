using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyProjectile : MonoBehaviour
{
    public float damage = 10f;
    public float speed = 10f;
    public float lifeTime = 5f;

    private Rigidbody2D rb;
    private Collider2D col;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col.isTrigger = true;
    }

    public void Init(Vector2 direction)
    {
        rb.linearVelocity = direction.normalized * speed;
        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if hit player
        if (other.CompareTag("Player"))
        {
            if (other.TryGetComponent(out PlayerController player))
            {
                player.AddDamage((int)damage);
            }
            
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
