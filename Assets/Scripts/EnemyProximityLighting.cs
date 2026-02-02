using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Dynamically adjusts enemy sprite brightness based on distance to player.
/// Enemies closer to the player appear brighter (more lit), creating a 
/// spotlight effect where enemies emerge from darkness as they approach.
/// </summary>
public class EnemyProximityLighting : MonoBehaviour
{
    [Header("Distance Settings")]
    [Tooltip("Maximum distance at which enemies are fully dark")]
    public float maxDistance = 15f;
    
    [Tooltip("Distance at which enemies reach full brightness")]
    public float fullBrightnessDistance = 2f;
    
    [Header("Brightness Settings")]
    [Tooltip("Minimum brightness when far from player (0 = black, 1 = normal)")]
    [Range(0f, 1f)]
    public float minBrightness = 0.2f;
    
    [Tooltip("Maximum brightness when close to player")]
    [Range(0.5f, 2f)]
    public float maxBrightness = 1.2f;
    
    [Header("Transition")]
    [Tooltip("How quickly brightness changes (higher = snappier)")]
    public float transitionSpeed = 8f;
    
    [Tooltip("Use smooth curve for transition")]
    public bool useSmoothCurve = true;
    
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private float currentBrightness = 1f;
    private Color originalColor;
    private bool hasOriginalColor = false;
    
    void Start()
    {
        FindPlayer();
        CacheSpriteRenderer();
    }
    
    void FindPlayer()
    {
        // Find player by tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }
    
    void CacheSpriteRenderer()
    {
        // Try to find sprite renderer on this object or children
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        
        if (spriteRenderer != null && !hasOriginalColor)
        {
            originalColor = spriteRenderer.color;
            hasOriginalColor = true;
        }
    }
    
    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }
        
        if (spriteRenderer == null)
        {
            CacheSpriteRenderer();
            return;
        }
        
        UpdateBrightness();
    }
    
    void UpdateBrightness()
    {
        float distance = Vector2.Distance(transform.position, player.position);
        
        // Calculate target brightness based on distance
        float normalizedDistance = Mathf.InverseLerp(fullBrightnessDistance, maxDistance, distance);
        
        // Apply curve for smoother transition
        if (useSmoothCurve)
        {
            normalizedDistance = SmoothStep(normalizedDistance);
        }
        
        float targetBrightness = Mathf.Lerp(maxBrightness, minBrightness, normalizedDistance);
        
        // Smoothly transition to target brightness
        currentBrightness = Mathf.Lerp(currentBrightness, targetBrightness, transitionSpeed * Time.deltaTime);
        
        // Apply brightness to sprite
        ApplyBrightness();
    }
    
    void ApplyBrightness()
    {
        if (spriteRenderer == null) return;
        
        // Preserve the original alpha and apply brightness to RGB
        Color newColor = new Color(
            originalColor.r * currentBrightness,
            originalColor.g * currentBrightness,
            originalColor.b * currentBrightness,
            spriteRenderer.color.a // Preserve current alpha (for fade effects)
        );
        
        spriteRenderer.color = newColor;
    }
    
    /// <summary>
    /// Smooth step function for natural-looking transitions
    /// </summary>
    float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
    
    /// <summary>
    /// Reset brightness to original when disabled or destroyed
    /// </summary>
    void OnDisable()
    {
        if (spriteRenderer != null && hasOriginalColor)
        {
            spriteRenderer.color = new Color(
                originalColor.r,
                originalColor.g,
                originalColor.b,
                spriteRenderer.color.a
            );
        }
    }
    
    /// <summary>
    /// Force update the original color (call after spawning if color changes)
    /// </summary>
    public void RefreshOriginalColor()
    {
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            hasOriginalColor = true;
        }
    }
    
    /// <summary>
    /// Set custom distance parameters
    /// </summary>
    public void SetDistanceParams(float fullBrightness, float maxDist)
    {
        fullBrightnessDistance = fullBrightness;
        maxDistance = maxDist;
    }
    
    /// <summary>
    /// Set brightness range
    /// </summary>
    public void SetBrightnessRange(float min, float max)
    {
        minBrightness = min;
        maxBrightness = max;
    }
}
