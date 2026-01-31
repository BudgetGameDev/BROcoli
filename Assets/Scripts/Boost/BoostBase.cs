using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public abstract class BoostBase : MonoBehaviour
{
<<<<<<< HEAD
    public abstract void Apply(PlayerController player);
=======
    public abstract float Amount { get; }

    [SerializeField] private Rigidbody2D _body;
    [SerializeField] private Collider2D _collider;

    public abstract void Apply(PlayerStats stats);
>>>>>>> 1a83212 (add boosts and playerstats)

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") == false)
        {
            return;
        }

        if (other.TryGetComponent(out PlayerStats stats) == false)
        {
            return;
        }

        Apply(stats);
        Destroy(gameObject);
    }
}
