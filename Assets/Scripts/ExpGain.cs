using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ExpGain : MonoBehaviour
{
    public float lifeTime = 30f;
    public int expAmountGain;
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

    public void Init(int expAmount)
    {
        expAmountGain = expAmount;
        Destroy(gameObject, lifeTime);
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
