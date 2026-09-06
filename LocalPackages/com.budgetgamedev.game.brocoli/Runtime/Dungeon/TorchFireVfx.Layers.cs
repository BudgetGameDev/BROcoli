using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed partial class TorchFireVfx
    {
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
                    : new ParticleSystem.MinMaxCurve(-0.018f, 0.018f);
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = layer == 0 || layer == 3 ? 0.004f : 0.035f;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.006f, 0.006f);
            velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.8f, rise * 1.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.004f, 0.004f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = layer == 2 ? 0.035f : 0.006f;
            noise.frequency = layer == 2 ? 0.8f : 0.45f;
            noise.scrollSpeed = 0.12f;
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
                    new GradientAlphaKey(layer == 1 ? 0.7f : 0.85f, 0.65f),
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
                        new Keyframe(0f, 0.96f),
                        new Keyframe(0.18f, 1f),
                        new Keyframe(1f, 0.94f)
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
            // Flame mesh feet stay in the fuel independently of camera pitch.
            renderer.pivot = Vector3.zero;
            if (layer == 0)
                ConfigureFlameSurface(particles, renderer, material, heightMax);
            else if (layer == 2)
                ConfigureEmbers(particles);
            renderer.allowRoll = layer == 1;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.sortingOrder =
                layer == 3 ? 2
                : layer == 1 ? 3
                : 5;
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
    }
}
