using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public abstract class BoostBase : MonoBehaviour
{
    public abstract float Amount { get; }
    
    /// <summary>
    /// Duration of the boost effect in seconds. 0 = permanent/instant.
    /// </summary>
    public virtual float Duration => 20f;

    [SerializeField] private Rigidbody2D _body;
    [SerializeField] private Collider2D _collider;
    [SerializeField] private float _lifetime = 30f;
    
    // Magnet attraction
    private Transform _playerTransform;
    private PlayerStats _playerStats;
    private const float MagnetSpeed = 12f;  // Speed to move towards player
    private const float MagnetAcceleration = 25f;  // How fast to accelerate
    private float _currentSpeed = 0f;

    /// <summary>
    /// Override this to specify which procedural sound to play for this boost.
    /// </summary>
    public abstract ProceduralBoostAudio.BoostSoundType BoostSoundType { get; }

    public abstract void Apply(PlayerStats stats);

    private void Start()
    {
        Destroy(gameObject, _lifetime);
        
        // Find player for magnet effect
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _playerStats = player.GetComponent<PlayerStats>();
        }
    }
    
    private void Update()
    {
        // Check for magnet attraction (skip for magnet boost itself to avoid recursion)
        if (this is MagnetBoost) return;
        
        if (_playerStats != null && _playerStats.HasMagnetActive && _playerTransform != null && _body != null)
        {
            float magnetRadius = _playerStats.MagnetRadius;
            float distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
            
            if (distanceToPlayer <= magnetRadius)
            {
                // Accelerate towards player
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, MagnetSpeed, MagnetAcceleration * Time.deltaTime);
                
                Vector2 direction = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
                _body.linearVelocity = direction * _currentSpeed;
            }
        }
        else if (_currentSpeed > 0f && _body != null)
        {
            // Magnet expired, slow down
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, MagnetAcceleration * Time.deltaTime);
            if (_currentSpeed <= 0.1f)
            {
                _body.linearVelocity = Vector2.zero;
                _currentSpeed = 0f;
            }
        }
    }

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

        Debug.Log($"Applying boost: {GetType().Name} with amount {Amount} for {Duration}s");

        // Play procedural audio for this boost type
        ProceduralBoostAudio.PlaySound(BoostSoundType);

        Apply(stats);
        Destroy(gameObject);
    }
}
