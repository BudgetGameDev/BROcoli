using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// Diablo 3: Reaper of Souls style camera.
/// - Smooth follow without centering/reset
/// - Subtle drift in movement direction that stays
/// - Responsive zoom for portrait/landscape
/// - Extra zoom on mobile devices
/// </summary>
public class CameraController : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int IsMobileBrowser();
#endif

    [Header("Target")]
    public Transform target;
    
    [Header("Follow Smoothing")]
    [Tooltip("How quickly camera follows target")]
    public float followSpeed = 5f;
    
    [Header("Look Ahead (Diablo style - no reset)")]
    [Tooltip("How much camera drifts in movement direction")]
    public float lookAheadAmount = 1f;
    
    [Tooltip("How quickly drift builds up")]
    public float driftSpeed = 1f;
    
    [Header("Responsive Zoom")]
    [Tooltip("Base field of view for landscape mode")]
    public float landscapeFOV = 35f;
    
    [Tooltip("Field of view for portrait mode (higher = more zoomed out)")]
    public float portraitFOV = 60f;
    
    [Tooltip("How quickly camera zooms between sizes")]
    public float zoomSpeed = 5f;
    
    [Header("Mobile Zoom")]
    [Tooltip("Extra zoom percentage on mobile (25 = 25% more zoom)")]
    public float mobileZoomPercent = 25f;
    
    [Tooltip("Force mobile zoom in editor for testing")]
    public bool forceMobileZoomInEditor = false;
    
    private Vector3 offset;
    private Vector3 currentDrift;
    private bool initialized;
    private Camera cam;
    private float targetFOV;
    private bool isMobile;

    void Start()
    {
        cam = GetComponent<Camera>();
        
        // Check if we're on mobile
        isMobile = CheckIsMobile();
        
        // Store current FOV as landscape FOV if not set
        if (cam != null && landscapeFOV <= 0)
        {
            landscapeFOV = cam.fieldOfView;
        }
        
        if (target != null)
        {
            // Preserve the offset set in the scene
            offset = transform.position - target.position;
            initialized = true;
        }
        
        // Initialize zoom immediately
        UpdateTargetZoom();
        if (cam != null)
        {
            cam.fieldOfView = targetFOV;
        }
    }
    
    private bool CheckIsMobile()
    {
        // Check Device Simulator first (works in editor when simulating mobile devices)
        if (UnityEngine.Device.SystemInfo.deviceType == DeviceType.Handheld)
        {
            Debug.Log("[CameraController] Mobile detected via Device.SystemInfo (Device Simulator or real device)");
            return true;
        }
        
#if UNITY_EDITOR
        if (forceMobileZoomInEditor)
        {
            Debug.Log("[CameraController] Forcing mobile zoom in editor");
            return true;
        }
        // In editor without device simulator, check regular SystemInfo as fallback
        if (SystemInfo.deviceType == DeviceType.Handheld)
        {
            Debug.Log("[CameraController] Mobile detected via SystemInfo");
            return true;
        }
        return false;
#elif UNITY_WEBGL
        bool result = IsMobileBrowser() == 1;
        Debug.Log($"[CameraController] IsMobileBrowser returned: {result}");
        return result;
#elif UNITY_IOS || UNITY_ANDROID
        return true;
#else
        return false;
#endif
    }

    void LateUpdate()
    {
        if (target == null) return;
        
        if (!initialized)
        {
            offset = transform.position - target.position;
            initialized = true;
        }
        
        // Update responsive zoom for portrait/landscape
        UpdateTargetZoom();
        if (cam != null)
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, zoomSpeed * Time.deltaTime);
        }
        
        // Diablo-style: very subtle drift that doesn't reset
        Vector3 targetDrift = Vector3.zero;
        if (target.TryGetComponent<PlayerController>(out var pc))
        {
            Vector2 move = pc.RawInput;
            if (move.sqrMagnitude > 0.1f)
            {
                // Only drift while moving, but don't reset when stopping
                targetDrift = new Vector3(move.x, move.y, 0f) * lookAheadAmount;
            }
            else
            {
                // Keep current drift when stopped (Diablo style)
                targetDrift = currentDrift;
            }
        }
        
        // Slowly drift towards target (or stay if not moving)
        currentDrift = Vector3.Lerp(currentDrift, targetDrift, driftSpeed * Time.deltaTime);
        
        // Smoothly follow target + offset + drift
        Vector3 desiredPosition = target.position + offset + currentDrift;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
    }
    
    void UpdateTargetZoom()
    {
        bool isPortrait = Screen.height > Screen.width;
        
        if (isPortrait)
        {
            // Portrait mode: use wider FOV to see more
            targetFOV = portraitFOV;
        }
        else
        {
            // Landscape mode: use base FOV
            targetFOV = landscapeFOV;
        }
        
        // Apply extra zoom on mobile (smaller FOV = more zoom)
        if (isMobile && mobileZoomPercent > 0)
        {
            float zoomFactor = 1f - (mobileZoomPercent / 100f);
            float originalFOV = targetFOV;
            targetFOV *= zoomFactor;
            Debug.Log($"[CameraController] Mobile zoom applied: {originalFOV}° -> {targetFOV}° ({mobileZoomPercent}% zoom)");
        }
    }
}
