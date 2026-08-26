using UnityEngine;

/// <summary>
/// Procedural walk animation for enemies - adds pulsating scale and rotation based on movement
/// </summary>
public class EnemyWalkAnimation : MonoBehaviour
{
    [Header("Pulsate Settings")]
    [SerializeField]
    private float pulsateSpeed = 8f; // How fast the pulsate cycle is

    [SerializeField]
    private float pulsateAmountX = 0.08f; // Horizontal squash/stretch

    [SerializeField]
    private float pulsateAmountY = 0.12f; // Vertical squash/stretch

    [SerializeField]
    private float pulsateAmountZ = 0.05f; // Depth squash/stretch (for 3D models)

    [Header("Spin/Wobble Settings")]
    [SerializeField]
    private float wobbleSpeed = 6f; // How fast the wobble is

    [SerializeField]
    private float wobbleAmount = 8f; // Max rotation degrees

    [SerializeField]
    private float spinSpeedMultiplier = 15f; // Spin based on movement speed

    [Header("Bounce Settings")]
    [SerializeField]
    private float bounceSpeed = 12f; // Vertical bounce frequency

    [SerializeField]
    private float bounceAmount = 0.15f; // Vertical bounce height

    [Header("References")]
    [SerializeField]
    private Transform visualTransform; // The child transform to animate (optional)

    private Vector3 baseScale;
    private Vector3 basePosition;
    private Rigidbody rb;
    private float timeOffset;
    private float currentSpin = 0f;
    private bool isInitialized = false;
    private bool attackOverride = false;

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
        if (isInitialized)
            return;

        rb = GetComponentInParent<Rigidbody>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();

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

        // Ensure a height offset for 3D models to prevent clipping into the ground
        if (Mathf.Approximately(basePosition.y, 0f))
        {
            basePosition.y = 0.5f;
        }

        isInitialized = true;
    }

    void Update()
    {
        if (visualTransform == null || attackOverride)
            return;

        float time = Time.time + timeOffset;
        float speed = rb != null ? rb.GroundVelocity().magnitude : 0f;

        // Intensity scales with movement speed (0.5 to 1.5 range)
        float intensity = Mathf.Clamp(0.5f + speed * 0.15f, 0.5f, 1.5f);

        // --- Contained squash ---
        // Never expand past the prefab's base footprint. Expansion here would
        // make the mesh poke outside its correctly scaled solid collider.
        float pulsatePhase = Mathf.Sin(time * pulsateSpeed) * intensity;
        float horizontalSquash = Mathf.InverseLerp(-1.5f, 1.5f, pulsatePhase);
        float verticalSquash = 1f - horizontalSquash;
        float scaleX = baseScale.x * (1f - horizontalSquash * pulsateAmountX);
        float scaleY = baseScale.y * (1f - verticalSquash * pulsateAmountY);
        float scaleZ = baseScale.z * (1f - Mathf.Abs(pulsatePhase) * pulsateAmountZ);

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

        visualTransform.localRotation = GroundPlane.YawRotation(wobble + currentSpin);

        // --- Vertical Bounce ---
        float bounce = Mathf.Abs(Mathf.Sin(time * bounceSpeed)) * bounceAmount * intensity;
        visualTransform.localPosition = basePosition + new Vector3(0f, bounce, 0f);
    }

    /// <summary>
    /// Gives the melee animation exclusive control of the visual transform.
    /// Without this, walk bounce and attack lunge overwrite each other every
    /// frame and can make the rendered enemy appear to shoot away from its body.
    /// </summary>
    public void SetAttackOverride(bool active)
    {
        if (!isInitialized)
            InitializeVisualTransform();

        attackOverride = active;
        if (visualTransform == null)
            return;

        visualTransform.localScale = baseScale;
        visualTransform.localPosition = basePosition;
        visualTransform.localRotation = Quaternion.identity;
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
