using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

public class Projectile : MonoBehaviour
{
    [SerializeField]
    private float _speed = 8f;

    [SerializeField]
    private float _lifetime = 3f;

    [SerializeField]
    Rigidbody _body;

    [SerializeField]
    Collider _collider;

    [Tooltip("Multiplies the shared damage-relative enemy knockback roll.")]
    [SerializeField, Min(0f)]
    private float _baseKnockbackMultiplier = 1f;

    private float _damage = 1;
    private Vector2 direction;
    private float _activeKnockbackMultiplier;

    private void Awake()
    {
        // Player projectiles are hit sensors, not physical bodies. Keeping the
        // collider as a trigger prevents them from imparting force to enemies.
        if (_collider == null)
            _collider = GetComponent<Collider>();
        if (_collider != null)
            _collider.isTrigger = true;
        if (_body == null)
            _body = GetComponent<Rigidbody>();

        _activeKnockbackMultiplier = _baseKnockbackMultiplier;
    }

    public void Init(Vector2 dir, float damage)
    {
        Init(dir, damage, _baseKnockbackMultiplier);
    }

    /// <summary>
    /// Allows a weapon to override the projectile prefab's base knockback.
    /// </summary>
    public void Init(Vector2 dir, float damage, float weaponKnockbackMultiplier)
    {
        _damage = damage;
        direction = dir.normalized;
        _activeKnockbackMultiplier = Mathf.Max(0f, weaponKnockbackMultiplier);
        Destroy(gameObject, _lifetime);
    }

    private void Update()
    {
        Vector3 displacement = (direction * _speed * Time.deltaTime).ToWorld();
        if (ProjectileWallCollision.Sweep(_collider, transform, displacement, out _))
        {
            DestroyOnImpact();
            return;
        }

        transform.position += displacement;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Projectile hit: " + other.name);
        if (ProjectileWallCollision.IsWall(other))
        {
            DestroyOnImpact();
            return;
        }

        if (other.CompareTag("Enemy") == false)
        {
            return;
        }

        if (other.TryGetComponent(out EnemyBase enemy))
        {
            // Pass knockback direction (same as projectile direction)
            enemy.TakeDamage(_damage, direction, _activeKnockbackMultiplier);

            // Play hit sound
            ProceduralProjectileHitAudio.PlayHit(
                transform.position,
                ProceduralProjectileHitAudio.HitSoundType.Energy,
                0.5f
            );

            Destroy(gameObject);
        }
    }

    private void DestroyOnImpact()
    {
        if (_collider != null)
            _collider.enabled = false;
        if (_body != null)
            _body.linearVelocity = Vector3.zero;
        Destroy(gameObject);
    }
}
