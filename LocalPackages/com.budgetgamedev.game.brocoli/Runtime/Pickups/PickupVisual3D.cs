using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Builds lightweight, shared low-poly meshes for XP and boost pickups.
    /// These objects are visual children only and add no physics colliders, so
    /// collection and magnet movement keep using the pickup's own Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class PickupVisual3D : MonoBehaviour
    {
        public enum ModelKind
        {
            Experience,
            Health,
            Damage,
            AttackSpeed,
            MovementSpeed,
            ExperienceBoost,
            DetectionRadius,
            Magnet,
            Hourglass,
            SprayRange,
            SprayWidth,
        }

        private const string ModelRootName = "PickupModel3D";

        // The crystal stands upright while boost faces lean toward the gameplay camera.
        // A shared 90-degree Y-up conversion left both models reading like flat sprites.
        private static readonly Quaternion ExperienceModelFrame = Quaternion.identity;
        private static readonly Quaternion BoostModelFrame = Quaternion.Euler(58f, 0f, 0f);
        private const int RadialSegments = 16;
        private const float ExperienceVisualScale = 0.62f;
        private const float BoostVisualScale = 0.9f;
        private const float ExperienceBaseHeight = 0.28f;
        private const float BoostBaseHeight = 0.62f;

        private static readonly Dictionary<Color32, Material> Materials =
            new Dictionary<Color32, Material>();

        private static Mesh boxMesh;
        private static Mesh cylinderMesh;
        private static Mesh ringMesh;
        private static Mesh gemMesh;

        private Transform modelRoot;
        private Transform spinTarget;
        private Vector3 modelBasePosition;
        private Vector3 modelBaseScale;
        private Quaternion modelBaseRotation;
        private Quaternion spinBaseRotation;
        private Vector3 rotationAxis = Vector3.forward;
        private float animationPhase;
        private float spinSpeed;
        private float attractionBlend;
        private bool attractionRequested;
        private bool initialized;

        public ModelKind Kind { get; private set; }
        public Transform ModelRoot => modelRoot;
        public bool IsAttracted => attractionRequested;
        public float AttractionBlend => attractionBlend;

        public void SetAttracted(bool attracted)
        {
            attractionRequested = attracted;
        }

        public void ResetAttraction()
        {
            attractionRequested = false;
            attractionBlend = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedResources()
        {
            Materials.Clear();
            BudgetGameDev.Shared.GameDisplaySettings.ValuesChanged -= RefreshGlowColors;
            GlowMaterials.Clear();
            boxMesh = null;
            cylinderMesh = null;
            ringMesh = null;
            gemMesh = null;
            glowSphereMesh = null;
        }

        public static PickupVisual3D AttachExperience(GameObject pickup)
        {
            return Attach(pickup, ModelKind.Experience);
        }

        public static PickupVisual3D AttachBoost(BoostBase boost)
        {
            return Attach(boost.gameObject, ModelKindForSound(boost.BoostSoundType));
        }

        internal static ModelKind ModelKindForSound(
            ProceduralBoostAudio.BoostSoundType soundType
        ) =>
            soundType switch
            {
                ProceduralBoostAudio.BoostSoundType.Health => ModelKind.Health,
                ProceduralBoostAudio.BoostSoundType.Damage => ModelKind.Damage,
                ProceduralBoostAudio.BoostSoundType.AttackSpeed => ModelKind.AttackSpeed,
                ProceduralBoostAudio.BoostSoundType.MovementSpeed => ModelKind.MovementSpeed,
                ProceduralBoostAudio.BoostSoundType.Experience => ModelKind.ExperienceBoost,
                ProceduralBoostAudio.BoostSoundType.DetectionRadius => ModelKind.DetectionRadius,
                ProceduralBoostAudio.BoostSoundType.SprayRange => ModelKind.SprayRange,
                ProceduralBoostAudio.BoostSoundType.SprayWidth => ModelKind.SprayWidth,
                ProceduralBoostAudio.BoostSoundType.Magnet => ModelKind.Magnet,
                ProceduralBoostAudio.BoostSoundType.TimeSlow => ModelKind.Hourglass,
                _ => ModelKind.ExperienceBoost,
            };

        private static PickupVisual3D Attach(GameObject pickup, ModelKind kind)
        {
            PickupVisual3D visual = pickup.GetComponent<PickupVisual3D>();
            if (visual == null)
                visual = pickup.AddComponent<PickupVisual3D>();

            visual.Initialize(kind);
            return visual;
        }

        private void Initialize(ModelKind kind)
        {
            if (initialized)
                return;

            initialized = true;
            Kind = kind;
            animationPhase = Mathf.Abs(GetInstanceID() % 1000) * 0.013f;
            spinSpeed = kind == ModelKind.Experience ? 72f : 34f;
            rotationAxis = kind == ModelKind.Experience ? Vector3.up : Vector3.forward;
            modelBasePosition =
                Vector3.up
                * (kind == ModelKind.Experience ? ExperienceBaseHeight : BoostBaseHeight);
            modelBaseScale =
                Vector3.one
                * (kind == ModelKind.Experience ? ExperienceVisualScale : BoostVisualScale);

            foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
                spriteRenderer.enabled = false;

            Transform existingRoot = transform.Find(ModelRootName);
            if (existingRoot != null)
            {
                modelRoot = existingRoot;
                modelRoot.localPosition = modelBasePosition;
                modelRoot.localScale = modelBaseScale;
                modelBaseRotation =
                    kind == ModelKind.Experience ? ExperienceModelFrame : BoostModelFrame;
                modelRoot.localRotation = modelBaseRotation;
                spinTarget =
                    kind == ModelKind.Experience ? modelRoot : modelRoot.Find("Token Face");
                spinBaseRotation =
                    kind == ModelKind.Experience
                        ? Quaternion.identity
                        : Quaternion.Euler(-60f, 0f, 0f);
                if (spinTarget != null)
                    spinTarget.localRotation = spinBaseRotation;
                EnsurePickupGlow();
                return;
            }

            GameObject rootObject = new GameObject(ModelRootName);
            rootObject.layer = gameObject.layer;
            modelRoot = rootObject.transform;
            modelRoot.SetParent(transform, false);
            modelBaseRotation =
                kind == ModelKind.Experience ? ExperienceModelFrame : BoostModelFrame;
            modelRoot.localPosition = modelBasePosition;
            modelRoot.localRotation = modelBaseRotation;
            modelRoot.localScale = modelBaseScale;

            if (kind == ModelKind.Experience)
                BuildExperienceCrystal();
            else
                BuildBoostToken(kind);

            EnsurePickupGlow();

            spinTarget = kind == ModelKind.Experience ? modelRoot : modelRoot.Find("Token Face");
            spinBaseRotation = spinTarget != null ? spinTarget.localRotation : Quaternion.identity;
        }

        private void Update()
        {
            if (!initialized || modelRoot == null)
                return;

            float time = Time.time + animationPhase;
            attractionBlend = Mathf.MoveTowards(
                attractionBlend,
                attractionRequested ? 1f : 0f,
                Time.deltaTime * 7f
            );
            float bobAmplitude = Mathf.Lerp(0.055f, 0.012f, attractionBlend);
            float bob = Mathf.Sin(time * 3.2f) * bobAmplitude;
            float pulse = 1f + Mathf.Sin(time * 4.1f) * 0.035f;
            float magneticWobble = Mathf.Sin(time * 16f) * 24f * attractionBlend;
            float angle = Mathf.Repeat(time * spinSpeed + magneticWobble, 360f);
            float pullScale = Mathf.Lerp(1f, 0.72f, attractionBlend);

            modelRoot.localPosition = modelBasePosition + Vector3.up * bob;
            modelRoot.localRotation = modelBaseRotation;
            modelRoot.localScale = modelBaseScale * pulse * pullScale;
            if (spinTarget != null)
                spinTarget.localRotation =
                    spinBaseRotation * Quaternion.AngleAxis(angle, rotationAxis);
        }
    }
}
