using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ExpGain : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public float lifeTime = 30f;
    public int expAmountGain;
    private Rigidbody2D rb;
    private Collider2D col;
    private GameStates gameStates;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        gameStates = FindFirstObjectByType<GameStates>();
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
            gameStates.exp += expAmountGain;
            Destroy(gameObject);
        }
    }
}
