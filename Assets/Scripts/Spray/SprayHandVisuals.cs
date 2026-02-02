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
    private SpriteRenderer handSprite;
    private SpriteRenderer sprayCanSprite;
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
    public SpriteRenderer HandSprite => handSprite;
    public SpriteRenderer SprayCanSprite => sprayCanSprite;

    public SprayHandVisuals(Transform parent)
    {
        sprayTransform = parent;
        playerTransform = parent.parent;
    }

    public void SetReferences(Transform hand, SpriteRenderer handSpr, SpriteRenderer canSpr)
    {
        handTransform = hand;
        handSprite = handSpr;
        sprayCanSprite = canSpr;
    }

    public void CreateHandVisuals()
    {
        GameObject handObj = new GameObject("SprayHand");
        handObj.transform.SetParent(sprayTransform);
        handObj.transform.localPosition = new Vector3(SpraySettings.HandOffset, 0, 0);
        handObj.transform.localRotation = Quaternion.identity;
        handTransform = handObj.transform;
        
        CreateHandSprite();
        CreateSprayCanSprite();
        CreateNozzleSprite();
    }

    private void CreateHandSprite()
    {
        GameObject obj = new GameObject("HandSprite");
        obj.transform.SetParent(handTransform);
        obj.transform.localPosition = SpraySettings.HandSpriteLocalPos;
        obj.transform.localScale = SpraySettings.HandSpriteScale;
        handSprite = obj.AddComponent<SpriteRenderer>();
        handSprite.sprite = CreateSimpleSprite();
        handSprite.color = SpraySettings.SkinToneColor;
        handSprite.sortingOrder = SpraySettings.HandSpriteSortingOrder;
    }

    private void CreateSprayCanSprite()
    {
        GameObject obj = new GameObject("SprayCanSprite");
        obj.transform.SetParent(handTransform);
        obj.transform.localPosition = SpraySettings.SprayCanLocalPos;
        obj.transform.localScale = SpraySettings.SprayCanScale;
        sprayCanSprite = obj.AddComponent<SpriteRenderer>();
        sprayCanSprite.sprite = CreateSimpleSprite();
        sprayCanSprite.color = SpraySettings.SprayCanColor;
        sprayCanSprite.sortingOrder = SpraySettings.SprayCanSortingOrder;
    }

    private void CreateNozzleSprite()
    {
        GameObject obj = new GameObject("Nozzle");
        obj.transform.SetParent(handTransform);
        obj.transform.localPosition = SpraySettings.NozzleLocalPos;
        obj.transform.localScale = SpraySettings.NozzleScale;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSimpleSprite();
        sr.color = SpraySettings.NozzleColor;
        sr.sortingOrder = SpraySettings.NozzleSortingOrder;
    }

    private Sprite CreateSimpleSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
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
        
        bool left = CurrentDirection.x < -0.1f;
        if (handSprite != null) handSprite.flipY = left;
        if (sprayCanSprite != null) sprayCanSprite.flipY = left;
    }

    public void SetVisible(bool visible)
    {
        if (handSprite != null) handSprite.enabled = visible;
        if (sprayCanSprite != null) sprayCanSprite.enabled = visible;
    }

    public bool HasHand => handTransform != null;
}
