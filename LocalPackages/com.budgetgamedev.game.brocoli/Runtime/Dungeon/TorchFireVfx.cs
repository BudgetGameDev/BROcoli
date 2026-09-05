using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// A compact, layered fire simulation. Authored torch transforms remain the source of the
    /// flame position; presentation is built at runtime so both render pipelines use the same fire.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class TorchFireVfx : MonoBehaviour
    {
        internal const int ParticleBudget = 112;
        private readonly List<Material> ownedMaterials = new();
        private bool initialized;

        private void Awake() => Initialize();

        internal void Initialize()
        {
            if (initialized)
                return;
            Shader shader = BrocoliShaders.Resolve(BrocoliShaders.TorchFire);
            if (shader == null)
                return;

            // Do not replace unrelated fireballs or emitters. Only a real torch's primary
            // fire material authorizes replacing its legacy particle stack.
            ParticleSystemRenderer primary = null;
            var legacy = GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var renderer in legacy)
                if (HdrTorchFlamePresentation.IsPrimaryFlame(renderer.sharedMaterial))
                    primary = renderer;
            if (primary == null)
                return;

            initialized = true;
            Vector3 origin = transform.InverseTransformPoint(primary.transform.position);
            foreach (var renderer in legacy)
            {
                renderer
                    .GetComponent<ParticleSystem>()
                    ?.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                renderer.enabled = false;
            }

            CreateLayer(
                shader,
                "Fire Core",
                origin,
                0,
                32,
                16f,
                0.45f,
                0.7f,
                0.31f,
                0.43f,
                0.66f,
                0.88f,
                0.28f,
                0.42f,
                0.23f,
                new Color(7f, 3f, 0.5f),
                HdrTorchFlamePresentation.PrimaryMaterialName
            );
            CreateLayer(
                shader,
                "Fire Tongues",
                origin + Vector3.up * 0.05f,
                0,
                28,
                11f,
                0.5f,
                0.85f,
                0.2f,
                0.32f,
                0.62f,
                0.9f,
                0.14f,
                0.24f,
                0.36f,
                new Color(3.2f, 1f, 0.05f),
                "DungeonTorchFireSecondary"
            );
            CreateLayer(
                shader,
                "Fire Smoke",
                origin + Vector3.up * 0.48f,
                1,
                28,
                4f,
                1.3f,
                2.1f,
                0.3f,
                0.45f,
                0.3f,
                0.45f,
                0.08f,
                0.15f,
                0.5f,
                new Color(0.065f, 0.052f, 0.04f),
                "DungeonTorchSmoke"
            );
            CreateLayer(
                shader,
                "Fire Embers",
                origin + Vector3.up * 0.15f,
                2,
                24,
                3f,
                0.85f,
                1.7f,
                0.012f,
                0.022f,
                0.025f,
                0.045f,
                0.55f,
                0.85f,
                0.9f,
                new Color(8f, 1.7f, 0.11f),
                "DungeonTorchEmbers"
            );
        }

        private void CreateLayer(
            Shader shader,
            string layerName,
            Vector3 origin,
            int layer,
            int capacity,
            float rate,
            float lifeMin,
            float lifeMax,
            float widthMin,
            float widthMax,
            float heightMin,
            float heightMax,
            float alphaMin,
            float alphaMax,
            float rise,
            Color color,
            string materialName
        )
        {
            var child = new GameObject(layerName) { layer = gameObject.layer };
            child.transform.SetParent(transform, false);
            child.transform.localPosition = origin;
            var particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 3f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.maxParticles = capacity;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(widthMin, widthMax);
            main.startSizeY = new ParticleSystem.MinMaxCurve(heightMin, heightMax);
            main.startSizeZ = 1f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, alphaMin),
                new Color(1f, 1f, 1f, alphaMax)
            );
            main.startRotation =
                layer == 1
                    ? new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI)
                    : new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = layer == 0 ? 0.045f : 0.075f;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.055f, 0.075f);
            velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.8f, rise * 1.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.045f, 0.045f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = layer == 2 ? 0.12f : 0.055f;
            noise.frequency = layer == 2 ? 1.8f : 1.2f;
            noise.scrollSpeed = 0.6f;
            noise.damping = true;
            noise.octaveCount = 1;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(
                        layer == 2 ? new Color(1f, 0.16f, 0.012f) : Color.white,
                        1f
                    ),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.14f),
                    new GradientAlphaKey(layer == 1 ? 0.7f : 0.45f, 0.55f),
                    new GradientAlphaKey(0f, 1f),
                }
            );
            fade.color = gradient;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                layer == 1
                    ? AnimationCurve.Linear(0f, 0.65f, 1f, 2.2f)
                    : new AnimationCurve(
                        new Keyframe(0f, 0.65f),
                        new Keyframe(0.18f, 1f),
                        new Keyframe(1f, 0.35f)
                    )
            );
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            var material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave,
            };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Layer", layer);
            ownedMaterials.Add(material);
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            // A bottom pivot seats flame sheets in the wick, smoke billows around its origin.
            renderer.pivot = layer == 0 ? new Vector3(0f, 0.45f, 0f) : Vector3.zero;
            renderer.allowRoll = layer == 1;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.sortingOrder = layer == 1 ? 3 : 5;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.SetActiveVertexStreams(
                new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position,
                    ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV,
                    ParticleSystemVertexStream.StableRandomX,
                }
            );
            if (Application.isPlaying)
                particles.Play();
        }

        private void OnDestroy()
        {
            foreach (Material material in ownedMaterials)
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
        }
    }
}
