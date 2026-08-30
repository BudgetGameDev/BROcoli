using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Small world-space splash emitted where a sanitizer particle is consumed.
    /// This is visual feedback only; damage remains owned by the weapon handlers.
    /// </summary>
    internal sealed class SprayHitSplash
    {
        private const int MaxContactsPerCallback = 6;
        private const int DropletsPerContact = 3;

        private readonly ParticleSystem particles;

        public SprayHitSplash(Transform parent)
        {
            GameObject splashObject = new GameObject("SprayHitSplash");
            splashObject.transform.SetParent(parent, false);
            particles = splashObject.AddComponent<ParticleSystem>();
            Configure();
        }

        public void Emit(IReadOnlyList<ParticleCollisionEvent> events, int eventCount)
        {
            if (events == null || eventCount <= 0)
                return;

            if (!particles.isPlaying)
                particles.Play();

            int contactCount = Mathf.Min(eventCount, MaxContactsPerCallback);
            for (int i = 0; i < contactCount; i++)
            {
                ParticleCollisionEvent collision = events[i];
                Vector3 normal =
                    collision.normal.sqrMagnitude > 0.001f
                        ? collision.normal.normalized
                        : -collision.velocity.normalized;
                if (normal.sqrMagnitude < 0.001f)
                    normal = Vector3.up;

                for (int j = 0; j < DropletsPerContact; j++)
                {
                    Vector3 scatter = Random.insideUnitSphere * 0.7f;
                    scatter.y = Mathf.Abs(scatter.y) + 0.2f;
                    Vector3 direction = (normal * 0.75f + scatter).normalized;
                    var emit = new ParticleSystem.EmitParams
                    {
                        position = collision.intersection + normal * 0.015f,
                        velocity = direction * Random.Range(0.35f, 1.15f),
                        startLifetime = Random.Range(0.1f, 0.2f),
                        startSize = Random.Range(0.025f, 0.065f),
                        startColor = Color.Lerp(
                            new Color(0.72f, 0.9f, 1f, 0.9f),
                            Color.white,
                            Random.value
                        ),
                    };
                    particles.Emit(emit, 1);
                }
            }
        }

        private void Configure()
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = 0.25f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.16f;
            main.startSpeed = 0f;
            main.startSize = 0.04f;
            main.maxParticles = 160;
            main.gravityModifier = 0.35f;

            var emission = particles.emission;
            emission.enabled = false;

            var shape = particles.shape;
            shape.enabled = false;

            SprayLayerFactory.SetupSizeOverLifetime(
                particles,
                (0f, 0.35f),
                (0.18f, 1f),
                (0.7f, 0.65f),
                (1f, 0f)
            );
            SprayLayerFactory.SetupColorOverLifetime(
                particles,
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.72f, 0.9f, 1f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.45f),
                    new GradientAlphaKey(0f, 1f),
                }
            );

            Material material = SprayMaterialCreator.GetSprayDropletMaterial();
            SprayLayerFactory.SetupBillboardRenderer(
                particles,
                SprayMaterialCreator.GetDropletTexture(),
                material,
                3
            );
            SprayLayerFactory.SetupStretchedRenderer(
                particles,
                lengthScale: 0.45f,
                velocityScale: 0.08f
            );
        }
    }
}
