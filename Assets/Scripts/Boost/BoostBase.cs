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

    [SerializeField]
    private Rigidbody2D _body;

    [SerializeField]
    private Collider2D _collider;

    [SerializeField]
    private float _lifetime = 30f;
    private PickupVisual3D _pickupVisual;
    private float _currentSpeed = 0f;
    private bool _localAttractionLocked;
    private bool _isCollected;

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
        _isCollected = false;
        ActivePickups.Add(this);
        ConfigureMagnetBody();
        _pickupVisual = PickupVisual3D.AttachBoost(this);
        PickupAttraction.Reset(_body, ref _currentSpeed, ref _localAttractionLocked, _pickupVisual);
    }

    private void OnDisable()
    {
        ActivePickups.Remove(this);
    }

    /// <summary>
    /// Returns true when another pickup already occupies an area approximately
    /// one camera viewport wide and tall around the proposed drop point.
    /// </summary>
    public static bool IsScreenAreaOccupied(
        Vector3 position,
        Camera camera,
        float fallbackWorldSize
    )
    {
        ActivePickups.RemoveWhere(pickup => pickup == null);

        foreach (BoostBase pickup in ActivePickups)
        {
            if (camera == null)
            {
                if (
                    ((Vector2)pickup.transform.position - (Vector2)position).sqrMagnitude
                    <= fallbackWorldSize * fallbackWorldSize
                )
                {
                    return true;
                }
                continue;
            }

            Vector3 candidateViewport = camera.WorldToViewportPoint(position);
            Vector3 pickupViewport = camera.WorldToViewportPoint(pickup.transform.position);
            if (
                Mathf.Abs(candidateViewport.x - pickupViewport.x) <= 1f
                && Mathf.Abs(candidateViewport.y - pickupViewport.y) <= 1f
            )
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
        PickupAttraction.UpdateMotion(
            _body,
            ref _currentSpeed,
            ref _localAttractionLocked,
            _pickupVisual
        );
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isCollected)
            return;

        PlayerStats stats = other.GetComponentInParent<PlayerStats>();
        if (stats == null)
            return;

        _isCollected = true;

        // Disable the pickup before activating its effect. Instant XP boosts
        // can open the level-up screen and pause time, so deferred destruction
        // alone would leave the model visibly suspended over the player.
        gameObject.SetActive(false);

        ProceduralBoostAudio.PlaySound(BoostSoundType);
        Apply(stats);
        Destroy(gameObject);
    }
}
