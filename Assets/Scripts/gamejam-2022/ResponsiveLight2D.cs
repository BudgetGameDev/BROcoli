using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Adjusts Light2D radius based on screen aspect ratio to maintain consistent fog of war
/// In portrait mode, reduces the light radius so you can't see too far vertically
/// </summary>
public class ResponsiveLight2D : MonoBehaviour
{
    [Header("Base Settings (for 16:9 landscape)")]
    [Tooltip("The outer radius to use in standard landscape (16:9)")]
    public float baseLandscapeOuterRadius = 12f;
    
    [Tooltip("The inner radius to use in standard landscape (16:9)")]
    public float baseLandscapeInnerRadius = 2f;
    
    [Header("Portrait Adjustment")]
    [Tooltip("In portrait, reduce radius by this factor to limit vertical visibility")]
    public float portraitRadiusMultiplier = 0.35f;
    
    [Header("Smoothing")]
    [Tooltip("How quickly the light radius adjusts")]
    public float adjustSpeed = 5f;
    
    private Light2D light2D;
    private float targetOuterRadius;
    private float targetInnerRadius;
    
    // Reference aspect ratio (16:9 landscape)
    private const float ReferenceAspect = 16f / 9f;
    
    void Start()
    {
        light2D = GetComponent<Light2D>();
        
        if (light2D == null)
        {
            Debug.LogError("[ResponsiveLight2D] No Light2D component found!");
            enabled = false;
            return;
        }
        
        // Store initial values if not set
        if (baseLandscapeOuterRadius <= 0)
        {
            baseLandscapeOuterRadius = light2D.pointLightOuterRadius;
        }
        if (baseLandscapeInnerRadius <= 0)
        {
            baseLandscapeInnerRadius = light2D.pointLightInnerRadius;
        }
        
        // Initialize immediately
        UpdateTargetRadius();
        light2D.pointLightOuterRadius = targetOuterRadius;
        light2D.pointLightInnerRadius = targetInnerRadius;
    }
    
    void Update()
    {
        if (light2D == null) return;
        
        UpdateTargetRadius();
        
        // Smoothly adjust radius
        light2D.pointLightOuterRadius = Mathf.Lerp(light2D.pointLightOuterRadius, targetOuterRadius, adjustSpeed * Time.deltaTime);
        light2D.pointLightInnerRadius = Mathf.Lerp(light2D.pointLightInnerRadius, targetInnerRadius, adjustSpeed * Time.deltaTime);
    }
    
    void UpdateTargetRadius()
    {
        float aspect = (float)Screen.width / Screen.height;
        bool isPortrait = aspect < 1f;
        
        if (isPortrait)
        {
            // Portrait mode: significantly reduce radius
            // The more vertical the screen, the smaller the radius
            float portraitFactor = Mathf.Clamp(aspect, 0.4f, 1f);
            float multiplier = Mathf.Lerp(portraitRadiusMultiplier, 1f, portraitFactor);
            
            targetOuterRadius = baseLandscapeOuterRadius * multiplier;
            targetInnerRadius = baseLandscapeInnerRadius * multiplier;
        }
        else
        {
            // Landscape: use base values
            // Could also scale slightly for ultra-wide, but keep it simple for now
            targetOuterRadius = baseLandscapeOuterRadius;
            targetInnerRadius = baseLandscapeInnerRadius;
        }
    }
}
