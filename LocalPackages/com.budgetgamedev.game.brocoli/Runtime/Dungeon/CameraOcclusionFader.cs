using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Fades whatever stands between the gameplay camera and a character who has
    /// to stay readable. Every decision - which occluder is in the way, when it
    /// lowers, and when it may stand back up - belongs to
    /// <see cref="WallVisibilityResolver"/>; this component only turns the answer
    /// into materials. Runtime material copies keep shared materials unchanged.
    ///
    /// Nothing here knows a wall from a barrel. An occluder qualifies by covering
    /// enough of a character to hide them, and it gives way across a fraction of
    /// its own measured height, so a prop added to the game later is handled by
    /// the rules already written rather than by rules written for it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)]
    public sealed partial class CameraOcclusionFader : MonoBehaviour
    {
        private const int MaxCastHits = 32;
        private const string FadeShaderResource = "Brocoli/Shaders/DungeonOcclusionFade";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int OcclusionFadeId = Shader.PropertyToID("_OcclusionFade");
        private static readonly int FadeStartYId = Shader.PropertyToID("_FadeStartY");
        private static readonly int FadeFeatherId = Shader.PropertyToID("_FadeFeather");

        [SerializeField]
        private Transform target;

        [Tooltip(
            "Which layers are searched for occluders. Anything on one of them that covers a "
                + "character enough to hide them is lowered, whatever kind of object it is."
        )]
        [SerializeField]
        private LayerMask occluderMask = 1 << 9;

        [SerializeField, Min(0.1f)]
        private float fadeSpeed = 6f;

        [Tooltip(
            "How much of an occluder's height its fade is blended across, as a fraction of "
                + "whichever is shorter: the occluder or the character it is hiding."
        )]
        [UnityEngine.Serialization.FormerlySerializedAs("wallFadeFeatherFraction")]
        [SerializeField, Range(0.02f, 0.35f)]
        private float fadeFeatherFraction = 0.12f;

        [Tooltip(
            "How much stays standing while an occluder is lowered, as a fraction of whichever is "
                + "shorter: the occluder or the character it hides. Measuring against the character "
                + "is what stops something far taller than the player hiding them after lowering."
        )]
        [UnityEngine.Serialization.FormerlySerializedAs("gatewayVisibleBaseFraction")]
        [UnityEngine.Serialization.FormerlySerializedAs("visibleWallBaseFraction")]
        [SerializeField, Range(0.25f, 0.65f)]
        private float visibleBaseFraction = 0.45f;

        [SerializeField, Min(0f)]
        private float targetHeight = 0.65f;

        [Header("Occlusion Stability")]
        [Tooltip(
            "A wall group that disappears from detection and returns within this interval is treated as boundary jitter."
        )]
        [SerializeField, Min(0f)]
        private float flickerReacquireWindow = 0.45f;

        [Tooltip(
            "How long a rapidly reacquired wall group remains lowered. Initial lowering and ordinary releases are unaffected."
        )]
        [SerializeField, Min(0f)]
        private float flickerStabilityHold = 0.65f;

        private readonly RaycastHit[] castHits = new RaycastHit[MaxCastHits];
        private readonly List<Renderer> hitRenderers = new();
        private readonly HashSet<Renderer> currentOccluders = new();
        private readonly Dictionary<Renderer, FadeState> fadeStates = new();
        private readonly List<Renderer> statesToRemove = new();
        private WallVisibilityResolver resolver;
        private Shader fadeShader;

        /// <summary>How many renderers are currently faded, for diagnostics.</summary>
        public int ActiveOccluderCount { get; private set; }

        /// <summary>How many occluders are lowered, for diagnostics.</summary>
        public int LoweredGroupCount { get; private set; }

        /// <summary>
        /// The decision layer, created on demand. A script reload while play mode
        /// is running rebuilds this component's plain fields without calling
        /// <see cref="Awake"/> again, so anything only assigned there comes back
        /// null and every later frame throws.
        /// </summary>
        private WallVisibilityResolver Resolver =>
            resolver ??= new WallVisibilityResolver(
                new WallVisibilityStateMachine.Settings(
                    releaseDelay,
                    flickerReacquireWindow,
                    flickerStabilityHold
                )
            );

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
            currentOccluders.Clear();
            UpdateOccludingGeometry();

            ActiveOccluderCount = currentOccluders.Count;
            LoweredGroupCount = Resolver.LoweredGroups.Count;
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
            foreach (Renderer renderer in currentOccluders)
            {
                if (!fadeStates.ContainsKey(renderer))
                {
                    fadeStates.Add(
                        renderer,
                        new FadeState(
                            renderer,
                            fadeShader,
                            fadeFeatherFraction,
                            visibleBaseFraction,
                            TargetBodyHeight
                        )
                    );
                }
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

                bool lowered = currentOccluders.Contains(renderer);
                state.Visibility = Mathf.MoveTowards(
                    state.Visibility,
                    lowered ? 0f : 1f,
                    fadeSpeed * Time.unscaledDeltaTime
                );

                if (lowered && !state.UsingFadedMaterials)
                {
                    renderer.sharedMaterials = state.FadedMaterials;
                    state.UsingFadedMaterials = true;
                }

                if (state.UsingFadedMaterials)
                    ApplyVisibility(state);

                if (!lowered && state.Visibility >= 0.999f)
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
            currentOccluders.Clear();
            ResetDetection();
            ActiveOccluderCount = 0;
            LoweredGroupCount = 0;
        }
    }
}
