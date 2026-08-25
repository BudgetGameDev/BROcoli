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
    
    // Global magnet attraction. Far-away drops move faster so even objects at
    // the edge of the map can visibly reach the player before the effect ends.
    private const float MinimumMagnetSpeed = 18f;
    private const float MaximumMagnetSpeed = 60f;
    private const float MagnetAcceleration = 120f;
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
        _currentSpeed = 0f;
        ConfigureMagnetBody();
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
    }

    private void ConfigureMagnetBody()
    {
        if (_body == null)
            _body = GetComponent<Rigidbody2D>();
        if (_collider == null)
            _collider = GetComponent<Collider2D>();

        if (_body != null)
        {
            _body.gravityScale = 0f;
            _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (_collider != null)
            _collider.isTrigger = true;
    }
    
    private void FixedUpdate()
    {
        Transform target = PlayerStats.ActiveMagnetTarget;

        // The target is global, so pickups already off-screen and pickups that
        // spawn after collection join the same attraction immediately.
        if (target != null && _body != null)
        {
            Vector2 toPlayer = (Vector2)target.position - _body.position;
            float distance = toPlayer.magnitude;
            if (distance <= 0.001f)
            {
                _body.linearVelocity = Vector2.zero;
                return;
            }

            float targetSpeed = Mathf.Clamp(
                distance * 4f,
                MinimumMagnetSpeed,
                MaximumMagnetSpeed);
            _currentSpeed = Mathf.MoveTowards(
                _currentSpeed,
                targetSpeed,
                MagnetAcceleration * Time.fixedDeltaTime);
            Vector2 direction = toPlayer / distance;
            float arrivalSpeed = distance * 0.75f / Mathf.Max(Time.fixedDeltaTime, 0.001f);
            _body.WakeUp();
            _body.linearVelocity = direction * Mathf.Min(_currentSpeed, arrivalSpeed);
        }
        else if (_currentSpeed > 0f && _body != null)
        {
            _currentSpeed = Mathf.MoveTowards(
                _currentSpeed,
                0f,
                MagnetAcceleration * Time.fixedDeltaTime);
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
        PlayerStats stats = other.GetComponentInParent<PlayerStats>();
        if (stats == null)
        {
            return;
        }

        Debug.Log($"Applying boost: {GetType().Name} with amount {Amount} for {Duration}s");

        // Play procedural audio for this boost type
        ProceduralBoostAudio.PlaySound(BoostSoundType);

        Apply(stats);
        Destroy(gameObject);
    }
}
