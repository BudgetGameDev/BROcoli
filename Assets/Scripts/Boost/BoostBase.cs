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
        Debug.Log($"BoostBase OnTriggerEnter2D with {other.name}");
        if (other.CompareTag("Player") == false)
        {
            return;
        }

        PlayerStats stats = other.GetComponentInChildren<PlayerStats>();

        if (stats == null)
        {
            Debug.Log("PlayerStats component not found on player!");
            return;
        }

        Debug.Log($"Applying boost: {GetType().Name} with amount {Amount}");
        Apply(stats);
        Destroy(gameObject);
    }
}
