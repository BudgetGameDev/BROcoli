using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Creates a spotlight cone effect on the player for dramatic highlights.
/// This light follows the player and creates a focused beam from above.
/// </summary>
public class PlayerSpotlight2D : MonoBehaviour
{
    [Header("Spotlight Settings")]
    [Tooltip("Inner angle of the spotlight cone (full intensity)")]
    [Range(0f, 360f)]
    public float innerAngle = 45f;
    
    [Tooltip("Outer angle of the spotlight cone (falloff edge)")]
    [Range(0f, 360f)]
    public float outerAngle = 90f;
    
    [Tooltip("Inner radius where light is at full intensity")]
    public float innerRadius = 0.5f;
    
    [Tooltip("Outer radius where light fades to zero")]
    public float outerRadius = 3f;
    
    [Header("Light Properties")]
    [Tooltip("Spotlight intensity (higher = brighter highlights)")]
    [Range(0f, 5f)]
    public float intensity = 1.5f;
    
    [Tooltip("Spotlight color (warm white for nice highlights)")]
    public Color lightColor = new Color(1f, 0.95f, 0.9f, 1f);
    
    [Tooltip("Falloff intensity (0 = sharp edge, 1 = soft falloff)")]
    [Range(0f, 1f)]
    public float falloffIntensity = 0.6f;
    
    [Header("Animation")]
    [Tooltip("Subtle intensity pulsing for dramatic effect")]
    public bool enablePulse = true;
    
    [Tooltip("Pulse speed")]
    public float pulseSpeed = 1.5f;
    
    [Tooltip("Pulse amount (percentage of base intensity)")]
    [Range(0f, 0.3f)]
    public float pulseAmount = 0.1f;
    
    [Header("Position Offset")]
    [Tooltip("Rotation of the spotlight (0 = pointing down from above)")]
    public float rotationAngle = 0f;
    
    private Light2D spotLight;
    private float baseIntensity;
    private float pulseTimer;
    
    void Awake()
    {
        SetupSpotlight();
    }
    
    void SetupSpotlight()
    {
        spotLight = GetComponent<Light2D>();
        
        if (spotLight == null)
        {
            spotLight = gameObject.AddComponent<Light2D>();
        }
        
        // Configure as a point light with angle (spotlight effect)
        spotLight.lightType = Light2D.LightType.Point;
        spotLight.pointLightInnerAngle = innerAngle;
        spotLight.pointLightOuterAngle = outerAngle;
        spotLight.pointLightInnerRadius = innerRadius;
        spotLight.pointLightOuterRadius = outerRadius;
        spotLight.intensity = intensity;
        spotLight.color = lightColor;
        spotLight.falloffIntensity = falloffIntensity;
        
        // Use blend style 0 (additive) for highlights
        spotLight.blendStyleIndex = 0;
        
        baseIntensity = intensity;
    }
    
    void Update()
    {
        if (spotLight == null) return;
        
        // Apply rotation for spotlight direction
        transform.localRotation = Quaternion.Euler(0f, 0f, rotationAngle);
        
        // Subtle pulse animation for dramatic effect
        if (enablePulse)
        {
            pulseTimer += Time.deltaTime * pulseSpeed;
            float pulse = 1f + Mathf.Sin(pulseTimer) * pulseAmount;
            spotLight.intensity = baseIntensity * pulse;
        }
    }
    
    /// <summary>
    /// Updates spotlight settings at runtime
    /// </summary>
    public void UpdateSettings(float newIntensity, Color newColor)
    {
        intensity = newIntensity;
        baseIntensity = newIntensity;
        lightColor = newColor;
        
        if (spotLight != null)
        {
            spotLight.intensity = intensity;
            spotLight.color = lightColor;
        }
    }
    
    /// <summary>
    /// Enables or disables the spotlight
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (spotLight != null)
        {
            spotLight.enabled = enabled;
        }
    }
    
#if UNITY_EDITOR
    void OnValidate()
    {
        // Update light settings in editor when values change
        if (spotLight != null)
        {
            spotLight.pointLightInnerAngle = innerAngle;
            spotLight.pointLightOuterAngle = outerAngle;
            spotLight.pointLightInnerRadius = innerRadius;
            spotLight.pointLightOuterRadius = outerRadius;
            spotLight.intensity = intensity;
            spotLight.color = lightColor;
            spotLight.falloffIntensity = falloffIntensity;
            baseIntensity = intensity;
        }
    }
#endif
}
