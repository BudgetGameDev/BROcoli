using UnityEngine;

/// <summary>
/// Handles hand and spray can visual creation, positioning, and animation.
/// Manages smooth rotation towards spray direction and isometric view adjustments.
/// </summary>
public class SprayHandVisuals
{
    private Transform handTransform;
    private SpriteRenderer handSprite;
    private SpriteRenderer sprayCanSprite;
    private Transform sprayTransform;
    
    // Animation state
    private float currentHandAngle = 0f;
    private float targetHandAngle = 0f;
    private Vector2 targetDirection = Vector2.right;
    private bool isAiming = false;

    public Transform HandTransform => handTransform;
    public SpriteRenderer HandSprite => handSprite;
    public SpriteRenderer SprayCanSprite => sprayCanSprite;

    public SprayHandVisuals(Transform parent)
    {
        sprayTransform = parent;
    }

    /// <summary>
    /// Set existing hand references (assigned via inspector)
    /// </summary>
    public void SetReferences(Transform hand, SpriteRenderer handSpr, SpriteRenderer canSpr)
    {
        handTransform = hand;
        handSprite = handSpr;
        sprayCanSprite = canSpr;
    }

    /// <summary>
    /// Create hand visuals programmatically
    /// </summary>
    public void CreateHandVisuals()
    {
        // Create hand container
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
        GameObject handSpriteObj = new GameObject("HandSprite");
        handSpriteObj.transform.SetParent(handTransform);
        handSpriteObj.transform.localPosition = SpraySettings.HandSpriteLocalPos;
        handSpriteObj.transform.localScale = SpraySettings.HandSpriteScale;
        
        handSprite = handSpriteObj.AddComponent<SpriteRenderer>();
        handSprite.sprite = CreateSimpleSprite();
        handSprite.color = SpraySettings.SkinToneColor;
        handSprite.sortingOrder = SpraySettings.HandSpriteSortingOrder;
    }

    private void CreateSprayCanSprite()
    {
        GameObject canSpriteObj = new GameObject("SprayCanSprite");
        canSpriteObj.transform.SetParent(handTransform);
        canSpriteObj.transform.localPosition = SpraySettings.SprayCanLocalPos;
        canSpriteObj.transform.localScale = SpraySettings.SprayCanScale;
        
        sprayCanSprite = canSpriteObj.AddComponent<SpriteRenderer>();
        sprayCanSprite.sprite = CreateSimpleSprite();
        sprayCanSprite.color = SpraySettings.SprayCanColor;
        sprayCanSprite.sortingOrder = SpraySettings.SprayCanSortingOrder;
    }

    private void CreateNozzleSprite()
    {
        GameObject nozzleObj = new GameObject("Nozzle");
        nozzleObj.transform.SetParent(handTransform);
        nozzleObj.transform.localPosition = SpraySettings.NozzleLocalPos;
        nozzleObj.transform.localScale = SpraySettings.NozzleScale;
        
        SpriteRenderer nozzleSprite = nozzleObj.AddComponent<SpriteRenderer>();
        nozzleSprite.sprite = CreateSimpleSprite();
        nozzleSprite.color = SpraySettings.NozzleColor;
        nozzleSprite.sortingOrder = SpraySettings.NozzleSortingOrder;
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

    /// <summary>
    /// Set the target direction for aiming animation
    /// </summary>
    public void SetTargetDirection(Vector2 direction)
    {
        targetDirection = direction;
        targetHandAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        isAiming = true;
    }

    /// <summary>
    /// Get current hand angle
    /// </summary>
    public float CurrentAngle => currentHandAngle;

    /// <summary>
    /// Get target hand angle
    /// </summary>
    public float TargetAngle => targetHandAngle;

    /// <summary>
    /// Get the current aim direction based on the hand's actual rotation (not the target)
    /// </summary>
    public Vector2 CurrentDirection => new Vector2(
        Mathf.Cos(currentHandAngle * Mathf.Deg2Rad),
        Mathf.Sin(currentHandAngle * Mathf.Deg2Rad)
    );

    /// <summary>
    /// Get the world position of the nozzle (where particles should emit from)
    /// </summary>
    public Vector3 GetNozzleWorldPosition()
    {
        if (handTransform == null || sprayTransform == null)
            return sprayTransform != null ? sprayTransform.position : Vector3.zero;
        
        // The nozzle is at HandOffset along the direction the hand is pointing
        // Plus the NozzleLocalPos offset within the hand
        Vector2 dir = CurrentDirection;
        Vector3 playerPos = sprayTransform.parent != null ? sprayTransform.parent.position : sprayTransform.position;
        
        // Calculate nozzle position: player position + hand offset in aim direction + nozzle tip offset
        float totalOffset = SpraySettings.HandOffset + SpraySettings.NozzleLocalPos.x;
        return new Vector3(
            playerPos.x + dir.x * totalOffset,
            playerPos.y + dir.y * totalOffset,
            playerPos.z + SpraySettings.VisualZOffset
        );
    }

    /// <summary>
    /// Check if hand has reached target angle (within tolerance)
    /// </summary>
    public bool IsNearTarget => Mathf.Abs(Mathf.DeltaAngle(currentHandAngle, targetHandAngle)) 
        < SpraySettings.AngleToleranceForFiring;

    /// <summary>
    /// Animate the hand rotation towards target direction
    /// </summary>
    /// <param name="aimDirection">Direction to aim towards</param>
    public void AnimateRotation(Vector2 aimDirection)
    {
        targetHandAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        
        float angleDiff = Mathf.DeltaAngle(currentHandAngle, targetHandAngle);
        float maxRotation = SpraySettings.HandRotationSpeed * Time.deltaTime;
        
        if (Mathf.Abs(angleDiff) <= maxRotation)
        {
            currentHandAngle = targetHandAngle;
        }
        else
        {
            currentHandAngle += Mathf.Sign(angleDiff) * maxRotation;
        }
        
        // Normalize angle
        if (currentHandAngle > 180f) currentHandAngle -= 360f;
        if (currentHandAngle < -180f) currentHandAngle += 360f;
        
        // Apply rotation and position to spray transform
        ApplyTransform(aimDirection);
    }

    /// <summary>
    /// Apply rotation and position offsets for isometric view
    /// </summary>
    private void ApplyTransform(Vector2 aimDirection)
    {
        sprayTransform.localRotation = Quaternion.Euler(0, 0, currentHandAngle);
        
        // Calculate position offsets for isometric camera visibility
        float yOffset = 0f;
        float zOffset = SpraySettings.VisualZOffset;
        
        if (aimDirection.y > 0.3f)
        {
            float upwardAmount = Mathf.InverseLerp(0.3f, 1f, aimDirection.y);
            yOffset = SpraySettings.IsometricYOffset * upwardAmount;
            zOffset = SpraySettings.VisualZOffset - (0.3f * upwardAmount);
        }
        
        sprayTransform.localPosition = new Vector3(0, yOffset, zOffset);
        
        UpdateHandFlip(aimDirection);
    }

    /// <summary>
    /// Update sprite direction and position based on spray direction.
    /// IMPORTANT: This also updates currentHandAngle so CurrentDirection stays in sync.
    /// </summary>
    public void UpdateDirection(Vector2 sprayDirection)
    {
        float angle = Mathf.Atan2(sprayDirection.y, sprayDirection.x) * Mathf.Rad2Deg;
        
        // CRITICAL: Sync currentHandAngle so CurrentDirection and GetNozzleWorldPosition work correctly
        currentHandAngle = angle;
        targetHandAngle = angle;
        
        sprayTransform.localRotation = Quaternion.Euler(0, 0, angle);
        
        // Calculate position offsets
        float yOffset = 0f;
        float zOffset = SpraySettings.VisualZOffset;
        
        if (sprayDirection.y > 0.3f)
        {
            float upwardAmount = Mathf.InverseLerp(0.3f, 1f, sprayDirection.y);
            yOffset = SpraySettings.IsometricYOffset * upwardAmount;
            zOffset = SpraySettings.VisualZOffset - (0.3f * upwardAmount);
        }
        
        sprayTransform.localPosition = new Vector3(0, yOffset, zOffset);
        
        if (handTransform != null)
        {
            handTransform.localPosition = new Vector3(SpraySettings.HandOffset, 0, 0);
        }
        
        UpdateHandFlip(sprayDirection);
    }

    private void UpdateHandFlip(Vector2 direction)
    {
        if (!SpraySettings.FlipHandWithDirection) return;
        
        bool pointingLeft = direction.x < -0.1f;
        
        if (handSprite != null) handSprite.flipY = pointingLeft;
        if (sprayCanSprite != null) sprayCanSprite.flipY = pointingLeft;
    }

    /// <summary>
    /// Set hand visibility
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (handSprite != null) handSprite.enabled = visible;
        if (sprayCanSprite != null) sprayCanSprite.enabled = visible;
    }

    /// <summary>
    /// Check if hand references exist
    /// </summary>
    public bool HasHand => handTransform != null;
}
