using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fades dungeon walls that sit between the gameplay camera and the player.
/// Runtime material copies keep the shared wall material unchanged.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)]
public sealed class CameraOcclusionFader : MonoBehaviour
{
    private const int MaxCastHits = 32;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField]
    private Transform target;

    [SerializeField]
    private LayerMask occluderMask = 1 << 9;

    [SerializeField, Range(0.1f, 0.8f)]
    private float occludedAlpha = 0.3f;

    [SerializeField, Min(0.1f)]
    private float fadeSpeed = 6f;

    [SerializeField, Range(0f, 2f)]
    private float castRadius = 0.8f;

    [SerializeField, Min(0f)]
    private float targetHeight = 0.65f;

    private sealed class FadeState
    {
        public readonly Renderer Renderer;
        public readonly Material[] OriginalMaterials;
        public readonly Material[] FadedMaterials;
        public readonly Color[] BaseColors;
        public float Alpha = 1f;
        public bool UsingFadedMaterials;

        public FadeState(Renderer renderer)
        {
            Renderer = renderer;
            OriginalMaterials = renderer.sharedMaterials;
            FadedMaterials = new Material[OriginalMaterials.Length];
            BaseColors = new Color[OriginalMaterials.Length];

            for (int i = 0; i < OriginalMaterials.Length; i++)
            {
                Material original = OriginalMaterials[i];
                if (original == null)
                    continue;

                var faded = new Material(original)
                {
                    name = $"{original.name} (Occlusion Fade)",
                    hideFlags = HideFlags.DontSave,
                };
                ConfigureTransparent(faded);
                FadedMaterials[i] = faded;
                BaseColors[i] = ReadColor(original);
            }
        }
    }

    private readonly RaycastHit[] castHits = new RaycastHit[MaxCastHits];
    private readonly List<Renderer> hitRenderers = new();
    private readonly HashSet<Renderer> currentOccluders = new();
    private readonly Dictionary<Renderer, FadeState> fadeStates = new();
    private readonly List<Renderer> statesToRemove = new();

    public int ActiveOccluderCount { get; private set; }

    private void Awake()
    {
        ResolveTarget();
        if (occluderMask.value == 0)
            occluderMask = LayerMask.GetMask("Wall");
    }

    private void LateUpdate()
    {
        ResolveTarget();
        currentOccluders.Clear();

        if (target != null)
            FindOccludingWalls();

        ActiveOccluderCount = currentOccluders.Count;
        UpdateFades();
    }

    private void ResolveTarget()
    {
        if (target != null)
            return;

        CameraController controller = GetComponent<CameraController>();
        if (controller != null && controller.target != null)
        {
            target = controller.target;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;
    }

    private void FindOccludingWalls()
    {
        Vector3 origin = transform.position;
        Vector3 targetPoint = target.position + Vector3.up * targetHeight;
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= Mathf.Epsilon)
            return;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            castRadius,
            toTarget / distance,
            castHits,
            distance,
            occluderMask,
            QueryTriggerInteraction.Ignore
        );
        for (int i = 0; i < hitCount; i++)
        {
            Collider wall = castHits[i].collider;
            if (wall == null || !IsStructuralOccluder(wall.name))
                continue;

            hitRenderers.Clear();
            wall.GetComponentsInChildren(false, hitRenderers);
            foreach (Renderer wallRenderer in hitRenderers)
            {
                if (wallRenderer != null && wallRenderer.enabled)
                    currentOccluders.Add(wallRenderer);
            }
        }
    }

    private static bool IsStructuralOccluder(string objectName)
    {
        return objectName.StartsWith("DungeonWall", System.StringComparison.Ordinal)
            || objectName.StartsWith("DungeonCorner", System.StringComparison.Ordinal)
            || objectName.StartsWith("DungeonGate", System.StringComparison.Ordinal);
    }

    private void UpdateFades()
    {
        foreach (Renderer renderer in currentOccluders)
        {
            if (!fadeStates.ContainsKey(renderer))
                fadeStates.Add(renderer, new FadeState(renderer));
        }

        statesToRemove.Clear();
        foreach (KeyValuePair<Renderer, FadeState> pair in fadeStates)
        {
            Renderer renderer = pair.Key;
            FadeState state = pair.Value;
            if (renderer == null)
            {
                DestroyFadedMaterials(state);
                statesToRemove.Add(renderer);
                continue;
            }

            bool occluded = currentOccluders.Contains(renderer);
            float desiredAlpha = occluded ? occludedAlpha : 1f;
            state.Alpha = Mathf.MoveTowards(
                state.Alpha,
                desiredAlpha,
                fadeSpeed * Time.unscaledDeltaTime
            );

            if (occluded && !state.UsingFadedMaterials)
            {
                renderer.sharedMaterials = state.FadedMaterials;
                state.UsingFadedMaterials = true;
            }

            if (state.UsingFadedMaterials)
                ApplyAlpha(state);

            if (!occluded && state.Alpha >= 0.999f)
            {
                renderer.sharedMaterials = state.OriginalMaterials;
                state.UsingFadedMaterials = false;
            }
        }

        foreach (Renderer renderer in statesToRemove)
            fadeStates.Remove(renderer);
    }

    private static void ApplyAlpha(FadeState state)
    {
        for (int i = 0; i < state.FadedMaterials.Length; i++)
        {
            Material material = state.FadedMaterials[i];
            if (material == null)
                continue;

            Color color = state.BaseColors[i];
            color.a *= state.Alpha;
            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, color);
            else if (material.HasProperty(ColorId))
                material.SetColor(ColorId, color);
        }
    }

    private static Color ReadColor(Material material)
    {
        if (material.HasProperty(BaseColorId))
            return material.GetColor(BaseColorId);
        return material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
    }

    private static void ConfigureTransparent(Material material)
    {
        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_Blend", 0f);
        SetFloatIfPresent(material, "_AlphaClip", 0f);
        SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloatIfPresent(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private void OnDisable()
    {
        foreach (FadeState state in fadeStates.Values)
        {
            if (state.Renderer != null && state.UsingFadedMaterials)
                state.Renderer.sharedMaterials = state.OriginalMaterials;
            DestroyFadedMaterials(state);
        }
        fadeStates.Clear();
        currentOccluders.Clear();
        ActiveOccluderCount = 0;
    }

    private static void DestroyFadedMaterials(FadeState state)
    {
        foreach (Material material in state.FadedMaterials)
        {
            if (material != null)
                Destroy(material);
        }
    }
}
