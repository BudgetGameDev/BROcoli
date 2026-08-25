using UnityEngine;

/// <summary>
/// THE SINGLE SOURCE OF TRUTH FOR SPRAY DIRECTION.
/// 
/// Hand tracks target, animates toward it, CurrentDirection is where we spray.
/// No separate "target direction" vs "animated direction" - just ONE direction.
/// </summary>
public class SprayHandVisuals
{
    private Transform handTransform;
    private SprayWeaponVisual3D weaponVisual;
    private Transform sprayTransform;
    private Transform playerTransform;
    
    // Target tracking
    private Transform targetTransform;
    private Vector2? predictedTargetPosition = null;
    private float maxRange = 3f;
    
    // Animation state
    private float currentHandAngle = 0f;
    private float targetHandAngle = 0f;

    public Transform HandTransform => handTransform;

    public SprayHandVisuals(Transform parent)
    {
        sprayTransform = parent;
        playerTransform = parent.parent;
    }

    public void CreateHandVisuals()
    {
        foreach (SpriteRenderer sprite in sprayTransform.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.enabled = false;

        handTransform = sprayTransform.Find("SprayHand");
        if (handTransform == null)
        {
            GameObject handObject = new GameObject("SprayHand");
            handTransform = handObject.transform;
            handTransform.SetParent(sprayTransform, false);
        }

        handTransform.localPosition = new Vector3(SpraySettings.HandOffset, 0f, 0f);
        handTransform.localRotation = Quaternion.identity;
        weaponVisual = SprayWeaponVisual3D.Attach(handTransform.gameObject);
    }

    // ==================== TARGET TRACKING ====================

    public void SetTarget(Transform target) { targetTransform = target; predictedTargetPosition = null; }
    public void SetTarget(Transform target, Vector2 predictedPos) { targetTransform = target; predictedTargetPosition = predictedPos; }
    public void ClearTarget() { targetTransform = null; predictedTargetPosition = null; }
    public void SetRange(float range) { maxRange = range; }

    /// <summary>
    /// Get the center position of the current target (from collider bounds or transform)
    /// </summary>
    private Vector2 GetTargetCenter()
    {
        if (targetTransform == null) return Vector2.zero;
        Collider2D col = targetTransform.GetComponent<Collider2D>();
        return (col != null && col.enabled) ? (Vector2)col.bounds.center : (Vector2)targetTransform.position;
    }
    
    public bool HasTarget => targetTransform != null && targetTransform.gameObject.activeInHierarchy;
    
    public bool IsTargetInRange
    {
        get
        {
            if (targetTransform == null || playerTransform == null) return false;
            // Measure distance from player center (consistent with aim calculation)
            Vector2 playerPos = (Vector2)playerTransform.position;
            float dist = Vector2.Distance(playerPos, GetTargetCenter());
            return dist <= maxRange && dist >= SpraySettings.MinTargetDistance;
        }
    }

    // ==================== DIRECTION (THE ONLY DIRECTION) ====================

    /// <summary>
    /// THE spray direction. Where hand points RIGHT NOW. Particles and damage use THIS.
    /// </summary>
    public Vector2 CurrentDirection => new Vector2(
        Mathf.Cos(currentHandAngle * Mathf.Deg2Rad),
        Mathf.Sin(currentHandAngle * Mathf.Deg2Rad)
    );

    public bool IsAimedAtTarget => Mathf.Abs(Mathf.DeltaAngle(currentHandAngle, targetHandAngle)) 
        < SpraySettings.AngleToleranceForFiring;

    public Vector3 GetNozzleWorldPosition()
    {
        Vector3 playerPos = playerTransform != null ? playerTransform.position : sprayTransform.position;
        Vector2 dir = CurrentDirection;
        float offset = SpraySettings.HandOffset + SpraySettings.NozzleLocalPos.x;
        return new Vector3(playerPos.x + dir.x * offset, playerPos.y + dir.y * offset, playerPos.z + SpraySettings.VisualZOffset);
    }

    // ==================== UPDATE (CALL EVERY FRAME) ====================

    public void Update()
    {
        // Always track target (no freezing)
        if (targetTransform != null && playerTransform != null && targetTransform.gameObject.activeInHierarchy)
        {
            // Get target center (use predicted position if available)
            Vector2 targetPos = predictedTargetPosition ?? GetTargetCenter();
            
            // Calculate aim direction from PLAYER CENTER to target
            // This is geometrically correct: since nozzle is offset ALONG the aim ray,
            // aiming from player center ensures the spray ray passes through the target
            Vector2 playerPos = (Vector2)playerTransform.position;
            Vector2 toTarget = targetPos - playerPos;
            
            if (toTarget.magnitude > 0.1f)
                targetHandAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        }

        // Animate toward target
        float diff = Mathf.DeltaAngle(currentHandAngle, targetHandAngle);
        float maxRot = SpraySettings.HandRotationSpeed * Time.deltaTime;
        currentHandAngle += Mathf.Abs(diff) <= maxRot ? diff : Mathf.Sign(diff) * maxRot;
        
        if (currentHandAngle > 180f) currentHandAngle -= 360f;
        if (currentHandAngle < -180f) currentHandAngle += 360f;
        
        ApplyTransform();
    }

    private void ApplyTransform()
    {
        sprayTransform.localRotation = Quaternion.Euler(0, 0, currentHandAngle);
        sprayTransform.localPosition = new Vector3(0, 0, SpraySettings.VisualZOffset);

        // The weapon is held by a detached, hovering hand. Keeping the float on
        // the visual child preserves the exact 2D aim and damage direction.
        float hover = Mathf.Sin(Time.time * SpraySettings.HandHoverSpeed) *
            SpraySettings.HandHoverAmplitude;
        handTransform.localPosition = new Vector3(SpraySettings.HandOffset, hover, 0f);
        
        bool left = CurrentDirection.x < -0.1f;
        weaponVisual?.SetFacingLeft(left);
    }

    public void SetVisible(bool visible)
    {
        weaponVisual?.SetVisible(visible);
    }

    public bool HasHand => handTransform != null;
}
