using UnityEngine;

/// <summary>
/// Procedural walk animation for enemies - adds pulsating scale and rotation based on movement
/// </summary>
public class EnemyWalkAnimation : MonoBehaviour
{
    [Header("Pulsate Settings")]
    [SerializeField] private float pulsateSpeed = 8f;         // How fast the pulsate cycle is
    [SerializeField] private float pulsateAmountX = 0.08f;    // Horizontal squash/stretch
    [SerializeField] private float pulsateAmountY = 0.12f;    // Vertical squash/stretch
    [SerializeField] private float pulsateAmountZ = 0.05f;    // Depth squash/stretch (for 3D models)
    
    [Header("Spin/Wobble Settings")]
    [SerializeField] private float wobbleSpeed = 6f;          // How fast the wobble is
    [SerializeField] private float wobbleAmount = 8f;         // Max rotation degrees
    [SerializeField] private float spinSpeedMultiplier = 15f; // Spin based on movement speed
    
    [Header("Bounce Settings")]
    [SerializeField] private float bounceSpeed = 12f;         // Vertical bounce frequency
    [SerializeField] private float bounceAmount = 0.15f;      // Vertical bounce height
    
    [Header("References")]
    [SerializeField] private Transform visualTransform;       // The child transform to animate (optional)
    
    private Vector3 baseScale;
    private Vector3 basePosition;
    private Rigidbody2D rb;
    private float timeOffset;
    private float currentSpin = 0f;
    private bool isInitialized = false;
    
    void Awake()
    {
        // Initialize in Awake so baseScale/basePosition are set before OnDisable can run (during pooling)
        InitializeVisualTransform();
    }
    
    void Start()
    {
        // Ensure initialization (in case Awake didn't complete for some reason)
        if (!isInitialized)
        {
            InitializeVisualTransform();
        }
        
        // Random offset so not all enemies animate in sync
        timeOffset = Random.Range(0f, Mathf.PI * 2f);
    }
    
    private void InitializeVisualTransform()
    {
        if (isInitialized) return;
        
        rb = GetComponentInParent<Rigidbody2D>();
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        
        // If no visual transform specified, try to find a child or use self
        if (visualTransform == null)
        {
            // Look for a child that might be the visual (like "enemy-0" or similar)
            if (transform.childCount > 0)
            {
                // Find the first child that isn't a Canvas
                foreach (Transform child in transform)
                {
                    if (child.GetComponent<Canvas>() == null)
                    {
                        visualTransform = child;
                        break;
                    }
                }
            }
            
            // If still null, animate this transform
            if (visualTransform == null)
                visualTransform = transform;
        }
        
        baseScale = visualTransform.localScale;
        
        // Safety check: if scale is zero, use Vector3.one as fallback
        if (baseScale.sqrMagnitude < 0.0001f)
        {
            baseScale = Vector3.one;
            visualTransform.localScale = Vector3.one;
        }
        
        basePosition = visualTransform.localPosition;
        
        // Ensure Z offset for 3D models to prevent clipping into background
        if (Mathf.Approximately(basePosition.z, 0f))
        {
            basePosition.z = -0.5f;
        }
        
        isInitialized = true;
    }
    
    void Update()
    {
        if (visualTransform == null) return;
        
        float time = Time.time + timeOffset;
        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        
        // Intensity scales with movement speed (0.5 to 1.5 range)
        float intensity = Mathf.Clamp(0.5f + speed * 0.15f, 0.5f, 1.5f);
        
        // --- Pulsating Scale ---
        // Creates a breathing/squash-stretch effect
        float pulsatePhase = Mathf.Sin(time * pulsateSpeed) * intensity;
        float scaleX = baseScale.x * (1f + pulsatePhase * pulsateAmountX);
        float scaleY = baseScale.y * (1f - pulsatePhase * pulsateAmountY); // Inverse for squash/stretch
        float scaleZ = baseScale.z * (1f + pulsatePhase * pulsateAmountZ);
        
        visualTransform.localScale = new Vector3(scaleX, scaleY, scaleZ);
        
        // --- Wobble/Spin Rotation ---
        // Base wobble
        float wobble = Mathf.Sin(time * wobbleSpeed) * wobbleAmount * intensity;
        
        // Add spin based on movement direction
        if (rb != null && speed > 0.5f)
        {
            // Spin in the direction of movement
            float targetSpin = rb.linearVelocity.x * spinSpeedMultiplier;
            currentSpin = Mathf.Lerp(currentSpin, targetSpin, Time.deltaTime * 5f);
        }
        else
        {
            currentSpin = Mathf.Lerp(currentSpin, 0f, Time.deltaTime * 3f);
        }
        
        visualTransform.localRotation = Quaternion.Euler(0f, 0f, wobble + currentSpin);
        
        // --- Vertical Bounce ---
        float bounce = Mathf.Abs(Mathf.Sin(time * bounceSpeed)) * bounceAmount * intensity;
        visualTransform.localPosition = basePosition + new Vector3(0f, bounce, 0f);
    }
    
    void OnDisable()
    {
        // Reset to base state when disabled
        // Only reset if baseScale was initialized (Start has run) - prevents setting scale to zero
        if (visualTransform != null && baseScale.sqrMagnitude > 0.0001f)
        {
            visualTransform.localScale = baseScale;
            visualTransform.localPosition = basePosition;
            visualTransform.localRotation = Quaternion.identity;
        }
    }
}
