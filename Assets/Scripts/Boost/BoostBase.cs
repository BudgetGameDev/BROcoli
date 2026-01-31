using UnityEngine;

public abstract class BoostBase : MonoBehaviour
{
    public abstract float Amount { get; }

    [SerializeField] private Rigidbody2D _body;
    [SerializeField] private Collider2D _collider;

    public abstract void Apply(PlayerController player);

    private void Awake()
    {
        _collider.isTrigger = true;
        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.gravityScale = 0f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") == false)
        {
            return;
        }

        if (other.TryGetComponent(out PlayerController player) == false)
        {
            return;
        }

        Apply(player);
        Destroy(gameObject);
    }
}
