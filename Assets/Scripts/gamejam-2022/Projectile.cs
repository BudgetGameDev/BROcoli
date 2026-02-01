using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float _speed = 8f;
    [SerializeField] private float _lifetime = 3f;
    [SerializeField] Rigidbody2D _body;
    [SerializeField] Collider2D _collider;

    private float _damage = 1;
    private Vector2 direction;

    public void Init(Vector2 dir, float damage)
    {
        _damage = damage;
        direction = dir.normalized;
        Destroy(gameObject, _lifetime);
    }

    private void Update()
    {
        transform.position += (Vector3)(direction * _speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Projectile hit: " + other.name);
        if (other.CompareTag("Enemy") == false)
        {
            return;
        }

        if (other.TryGetComponent(out EnemyBase enemy))
        {
            // Pass knockback direction (same as projectile direction)
            enemy.TakeDamage(_damage, direction);
            
            // Play hit sound
            ProceduralProjectileHitAudio.PlayHit(transform.position, ProceduralProjectileHitAudio.HitSoundType.Energy, 0.5f);
            
            Destroy(gameObject);
        }
    }
}
