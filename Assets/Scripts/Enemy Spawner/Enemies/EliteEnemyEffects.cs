using UnityEngine;

/// <summary>
/// Handles elite enemy visual effects (glow, tint, scale).
/// Separated from EnemyBase to keep files under 300 LOC.
/// </summary>
public class EliteEnemyEffects : MonoBehaviour
{
    private GameObject glowEffect;
    private SpriteRenderer mainSpriteRenderer;
    private Color originalColor;
    
    [Header("Elite Visual Settings")]
    public Color glowColor = new Color(1f, 0.85f, 0.2f, 0.4f);
    public Color tintColor = new Color(1f, 0.9f, 0.5f, 1f);
    public float glowScale = 1.3f;
    
    public void ApplyEliteVisuals()
    {
        mainSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (mainSpriteRenderer == null) return;
        
        originalColor = mainSpriteRenderer.color;
        CreateGlowEffect();
        ApplyTint();
    }
    
    private void CreateGlowEffect()
    {
        glowEffect = new GameObject("EliteGlow");
        glowEffect.transform.SetParent(transform, false);
        glowEffect.transform.localPosition = Vector3.zero;
        
        SpriteRenderer glowSr = glowEffect.AddComponent<SpriteRenderer>();
        glowSr.sprite = mainSpriteRenderer.sprite;
        glowSr.sortingOrder = mainSpriteRenderer.sortingOrder - 1;
        glowSr.color = glowColor;
        glowEffect.transform.localScale = Vector3.one * glowScale;
    }
    
    private void ApplyTint()
    {
        if (mainSpriteRenderer != null)
        {
            mainSpriteRenderer.color = tintColor;
        }
    }
    
    public void RemoveEliteVisuals()
    {
        if (glowEffect != null)
        {
            Destroy(glowEffect);
            glowEffect = null;
        }
        
        if (mainSpriteRenderer != null)
        {
            mainSpriteRenderer.color = originalColor;
        }
    }
    
    void OnDestroy()
    {
        RemoveEliteVisuals();
    }
}
