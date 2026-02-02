using UnityEngine;

/// <summary>
/// Applies per-instance color tint variations to enemy renderers
/// using MaterialPropertyBlock to avoid material duplication.
/// Works with URP Lit shader by tinting the _BaseColor property.
/// </summary>
public class EnemyColorVariant : MonoBehaviour
{
    [System.Serializable]
    public struct Variant
    {
        public Color tint;
        
        public Variant(Color c) { tint = c; }
    }

    [Header("Renderers (auto-populated if empty)")]
    [SerializeField] private Renderer[] renderers;

    [Header("Color Variants - Tints that multiply with base texture")]
    [SerializeField] private Variant[] variants = new Variant[]
    {
        new Variant(new Color(1.0f, 1.0f, 1.0f, 1.0f)),    // Original (white = no change)
        new Variant(new Color(1.4f, 0.7f, 0.5f, 1.0f)),    // Strong warm/orange tint
        new Variant(new Color(0.5f, 0.7f, 1.4f, 1.0f)),    // Strong cool/blue tint
        new Variant(new Color(1.3f, 1.3f, 0.5f, 1.0f)),    // Strong yellow tint
        new Variant(new Color(1.2f, 0.5f, 1.3f, 1.0f)),    // Strong purple tint
        new Variant(new Color(0.5f, 1.3f, 0.6f, 1.0f)),    // Strong green tint
        new Variant(new Color(1.4f, 0.6f, 0.7f, 1.0f)),    // Strong pink/red tint
    };

    [Header("Selection")]
    [SerializeField] private int variantIndex = 0;
    [SerializeField] private bool randomizeOnEnable = true;
    [SerializeField] private int randomSeed = 0;

    // URP Lit shader uses _BaseColor for the albedo tint
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock mpb;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);
    }

    void OnEnable()
    {
        if (variants == null || variants.Length == 0) return;

        int i = variantIndex;

        if (randomizeOnEnable)
        {
            // Use instance ID as seed for consistent randomization per object
            int seed = (randomSeed != 0) ? randomSeed : gameObject.GetInstanceID();
            var rng = new System.Random(seed);
            i = rng.Next(0, variants.Length);
        }

        Apply(variants[i]);
    }

    /// <summary>
    /// Apply a specific color variant to all renderers
    /// </summary>
    public void Apply(Variant v)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;

            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, v.tint);
            mpb.SetColor(ColorId, v.tint);
            r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>
    /// Apply variant by index
    /// </summary>
    public void ApplyVariant(int index)
    {
        if (variants == null || index < 0 || index >= variants.Length) return;
        Apply(variants[index]);
    }

    /// <summary>
    /// Apply a random variant
    /// </summary>
    public void ApplyRandomVariant()
    {
        if (variants == null || variants.Length == 0) return;
        int i = Random.Range(0, variants.Length);
        Apply(variants[i]);
    }
}
