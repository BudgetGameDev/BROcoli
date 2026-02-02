using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Red vignette pulse overlay that triggers when player takes damage.
/// Creates itself programmatically - no scene setup required.
/// Intensity scales with damage percentage for impactful feedback.
/// </summary>
public class DamageVignette : MonoBehaviour
{
    // Vignette parameters
    private const float BaseDuration = 0.4f;
    private const float MaxDuration = 0.8f;
    private const float BaseAlpha = 0.25f;
    private const float MaxAlpha = 0.7f;
    private const int TextureSize = 256;

    private Image _vignetteImage;
    private float _currentAlpha;
    private float _targetAlpha;
    private float _pulseTimer;
    private float _pulseDuration;
    private bool _isPulsing;

    // Singleton for easy access
    private static DamageVignette _instance;
    public static DamageVignette Instance => _instance;

    private void Awake()
    {
        _instance = this;
        CreateVignetteOverlay();
    }

    private void Update()
    {
        if (!_isPulsing) return;

        _pulseTimer += Time.deltaTime;
        float t = _pulseTimer / _pulseDuration;

        if (t >= 1f)
        {
            _isPulsing = false;
            _currentAlpha = 0f;
        }
        else
        {
            // Smooth pulse: quick fade in, slow fade out
            float fadeIn = Mathf.Clamp01(t * 4f); // 0-0.25 of duration
            float fadeOut = 1f - Mathf.Clamp01((t - 0.25f) / 0.75f); // 0.25-1.0 of duration
            _currentAlpha = _targetAlpha * fadeIn * fadeOut;
        }

        if (_vignetteImage != null)
        {
            Color c = _vignetteImage.color;
            c.a = _currentAlpha;
            _vignetteImage.color = c;
        }
    }

    private void CreateVignetteOverlay()
    {
        // Find existing Canvas or create one
        Canvas canvas = FindExistingGameCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("DamageVignette: No Canvas found - creating overlay canvas");
            canvas = CreateOverlayCanvas();
        }

        // Create vignette GameObject
        GameObject vignetteGO = new GameObject("DamageVignette");
        vignetteGO.transform.SetParent(canvas.transform, false);

        // Add Image component
        _vignetteImage = vignetteGO.AddComponent<Image>();
        _vignetteImage.sprite = CreateVignetteSprite();
        _vignetteImage.color = new Color(0.55f, 0f, 0f, 0f); // Dark blood-red, start transparent
        _vignetteImage.raycastTarget = false;

        // Stretch to fill screen
        RectTransform rt = vignetteGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Ensure it renders on top of other UI
        vignetteGO.transform.SetAsLastSibling();
    }

    private Canvas FindExistingGameCanvas()
    {
        // Find all canvases and prefer ScreenSpaceOverlay
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && 
                canvas.gameObject.name == "Canvas")
            {
                return canvas;
            }
        }
        // Fallback to any overlay canvas
        foreach (var canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return canvas;
            }
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }

    private Canvas CreateOverlayCanvas()
    {
        GameObject canvasGO = new GameObject("DamageVignetteCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Render on top
        canvasGO.AddComponent<CanvasScaler>();
        return canvas;
    }

    private Sprite CreateVignetteSprite()
    {
        // Create radial gradient texture programmatically
        Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(TextureSize / 2f, TextureSize / 2f);
        float maxDist = TextureSize * 0.5f;

        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                // Create soft edge vignette - transparent in center, opaque at edges
                float t = Mathf.Clamp01(dist / maxDist);
                // Power curve for softer center, harder edges
                float alpha = Mathf.Pow(t, 2.5f);
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Trigger a damage pulse with given intensity (0-1).
    /// Intensity typically maps to damage percentage of max health.
    /// </summary>
    /// <param name="intensity">Pulse intensity from 0 (subtle) to 1 (maximum).</param>
    public void TriggerPulse(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);
        
        _targetAlpha = Mathf.Lerp(BaseAlpha, MaxAlpha, intensity);
        _pulseDuration = Mathf.Lerp(BaseDuration, MaxDuration, intensity);
        _pulseTimer = 0f;
        _isPulsing = true;
    }

    /// <summary>
    /// Static helper to trigger pulse from anywhere.
    /// </summary>
    public static void Pulse(float intensity)
    {
        if (_instance != null)
        {
            _instance.TriggerPulse(intensity);
        }
    }
}
