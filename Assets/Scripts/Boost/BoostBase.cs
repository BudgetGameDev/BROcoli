using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public abstract class BoostBase : MonoBehaviour
{
    private static readonly HashSet<BoostBase> ActivePickups = new HashSet<BoostBase>();

    public abstract float Amount { get; }
    public virtual float DropWeight => 1f;
    
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
    private const float MagnetSpeed = 20f;
    private const float MagnetAcceleration = 40f;
    private float _currentSpeed = 0f;

    /// <summary>
    /// Override this to specify which procedural sound to play for this boost.
    /// </summary>
    public abstract ProceduralBoostAudio.BoostSoundType BoostSoundType { get; }

    public abstract void Apply(PlayerStats stats);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPickupRegistry()
    {
        ActivePickups.Clear();
    }

    private void OnEnable()
    {
        ActivePickups.Add(this);
    }

    private void OnDisable()
    {
        ActivePickups.Remove(this);
    }

    /// <summary>
    /// Returns true when another pickup already occupies an area approximately
    /// one camera viewport wide and tall around the proposed drop point.
    /// </summary>
    public static bool IsScreenAreaOccupied(Vector3 position, Camera camera, float fallbackWorldSize)
    {
        ActivePickups.RemoveWhere(pickup => pickup == null);

        foreach (BoostBase pickup in ActivePickups)
        {
            if (camera == null)
            {
                if (((Vector2)pickup.transform.position - (Vector2)position).sqrMagnitude
                    <= fallbackWorldSize * fallbackWorldSize)
                {
                    return true;
                }
                continue;
            }

            Vector3 candidateViewport = camera.WorldToViewportPoint(position);
            Vector3 pickupViewport = camera.WorldToViewportPoint(pickup.transform.position);
            if (Mathf.Abs(candidateViewport.x - pickupViewport.x) <= 1f
                && Mathf.Abs(candidateViewport.y - pickupViewport.y) <= 1f)
            {
                return true;
            }
        }

        return false;
    }

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
        // The magnet intentionally reaches beyond the visible screen and pulls
        // every pickup type, including another magnet pickup.
        if (_playerStats != null && _playerStats.HasMagnetActive && _playerTransform != null && _body != null)
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, MagnetSpeed, MagnetAcceleration * Time.deltaTime);
            Vector2 direction = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
            _body.linearVelocity = direction * _currentSpeed;
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
