using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades dungeon walls that sit between the gameplay camera and the player.
/// Runtime material copies keep the shared wall material unchanged.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)]
public sealed partial class CameraOcclusionFader : MonoBehaviour
{
    private const int MaxCastHits = 32;
    private const string FadeShaderResource = "Shaders/DungeonOcclusionFade";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int OcclusionFadeId = Shader.PropertyToID("_OcclusionFade");
    private static readonly int FadeStartYId = Shader.PropertyToID("_FadeStartY");
    private static readonly int FadeFeatherId = Shader.PropertyToID("_FadeFeather");

    [SerializeField]
    private Transform target;

    [SerializeField]
    private LayerMask occluderMask = 1 << 9;

    [SerializeField, Min(0.1f)]
    private float fadeSpeed = 6f;

    [SerializeField, Range(0.02f, 0.35f)]
    private float wallFadeFeatherFraction = 0.12f;

    [UnityEngine.Serialization.FormerlySerializedAs("gatewayVisibleBaseFraction")]
    [SerializeField, Range(0.25f, 0.65f)]
    private float visibleWallBaseFraction = 0.45f;

    [SerializeField, Min(0f)]
    private float targetHeight = 0.65f;

    private sealed class FadeState
    {
        public readonly Renderer Renderer;
        public readonly Material[] OriginalMaterials;
        public readonly Material[] FadedMaterials;
        public readonly Color[] BaseColors;
        public float Visibility = 1f;
        public float LastOccludedTime = float.NegativeInfinity;
        public bool UsingFadedMaterials;

        public FadeState(
            Renderer renderer,
            Shader fadeShader,
            float featherFraction,
            float visibleBaseFraction
        )
        {
            Renderer = renderer;
            OriginalMaterials = renderer.sharedMaterials;
            FadedMaterials = new Material[OriginalMaterials.Length];
            BaseColors = new Color[OriginalMaterials.Length];
            DungeonOcclusionSection section =
                renderer.GetComponentInParent<DungeonOcclusionSection>();
            bool structural = section != null || IsFreestandingColumn(renderer);
            float fadeReferenceMinY = renderer.bounds.min.y;
            float fadeReferenceHeight = renderer.bounds.size.y;
            if (
                section != null
                && section.TryGetGatewayFadeReference(
                    renderer,
                    out float gatewayMinimumY,
                    out float gatewayHeight
                )
            )
            {
                fadeReferenceMinY = gatewayMinimumY;
                fadeReferenceHeight = gatewayHeight;
            }
            float fadeFeather = structural
                ? Mathf.Max(0.02f, fadeReferenceHeight * featherFraction)
                : 0.02f;
            // Gateway crowns and grates use their adjoining wall's absolute
            // cutoff instead of a fraction of their different renderer heights.
            float fadeStart = structural
                ? fadeReferenceMinY + fadeReferenceHeight * visibleBaseFraction
                : renderer.bounds.min.y - fadeFeather;

            for (int i = 0; i < OriginalMaterials.Length; i++)
            {
                Material original = OriginalMaterials[i];
                if (original == null)
                    continue;

                Material faded;
                if (fadeShader != null)
                {
                    faded = new Material(fadeShader);
                    faded.CopyMatchingPropertiesFromMaterial(original);
                    faded.renderQueue = original.renderQueue;
                    faded.enableInstancing = original.enableInstancing;
                    faded.SetFloat(FadeStartYId, fadeStart);
                    faded.SetFloat(FadeFeatherId, fadeFeather);
                    faded.SetFloat(OcclusionFadeId, 0f);
                    faded.SetShaderPassEnabled("ShadowCaster", false);
                }
                else
                {
                    faded = new Material(original);
                    ConfigureTransparent(faded);
                }
                faded.name = $"{original.name} (Occlusion Fade)";
                faded.hideFlags = HideFlags.DontSave;
                FadedMaterials[i] = faded;
                BaseColors[i] = ReadColor(original);
            }
        }
    }

    private readonly RaycastHit[] castHits = new RaycastHit[MaxCastHits];
    private readonly List<Renderer> hitRenderers = new();
    private readonly HashSet<DungeonOcclusionSection> currentSections = new();
    private readonly HashSet<Renderer> currentOccluders = new();
    private readonly Dictionary<Renderer, FadeState> fadeStates = new();
    private readonly List<Renderer> statesToRemove = new();
    private Shader fadeShader;

    public int ActiveOccluderCount { get; private set; }

    private void Awake()
    {
        ResolveTarget();
        fadeShader = Resources.Load<Shader>(FadeShaderResource);
        if (occluderMask.value == 0)
            occluderMask = LayerMask.GetMask("Wall");
    }

    private void LateUpdate()
    {
        ResolveTarget();
        currentSections.Clear();
        currentOccluders.Clear();

        if (target != null)
            FindOccludingGeometry();

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

    private void UpdateFades()
    {
        float now = Time.unscaledTime;
        foreach (Renderer renderer in currentOccluders)
        {
            if (!fadeStates.TryGetValue(renderer, out FadeState state))
            {
                state = new FadeState(
                    renderer,
                    fadeShader,
                    wallFadeFeatherFraction,
                    visibleWallBaseFraction
                );
                fadeStates.Add(renderer, state);
            }
            state.LastOccludedTime = now;
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

            bool occluded = now - state.LastOccludedTime <= releaseDelay;
            float desiredVisibility = occluded ? 0f : 1f;
            state.Visibility = Mathf.MoveTowards(
                state.Visibility,
                desiredVisibility,
                fadeSpeed * Time.unscaledDeltaTime
            );

            if (occluded && !state.UsingFadedMaterials)
            {
                renderer.sharedMaterials = state.FadedMaterials;
                state.UsingFadedMaterials = true;
            }

            if (state.UsingFadedMaterials)
                ApplyVisibility(state);

            if (!occluded && state.Visibility >= 0.999f)
            {
                renderer.sharedMaterials = state.OriginalMaterials;
                state.UsingFadedMaterials = false;
            }
        }

        foreach (Renderer renderer in statesToRemove)
            fadeStates.Remove(renderer);
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
        currentSections.Clear();
        currentOccluders.Clear();
        ResetDetection();
        ActiveOccluderCount = 0;
    }
}
