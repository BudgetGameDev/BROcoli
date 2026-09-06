using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed partial class TorchFireVfx
    {
        private void ConfigureEmbers(ParticleSystem particles)
        {
            emberParticles = particles;
            emberDungeon = GetComponentInParent<DungeonManager>();
            combustionRandom = new System.Random(GetEntityId().GetHashCode());
            nextCrackle = RandomCombustion(0.15f, 0.8f);
            var main = particles.main;
            main.prewarm = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.65f, 0.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            var shape = particles.shape;
            shape.enabled = false;
            var emission = particles.emission;
            emission.enabled = false; // One combustion event drives both Emit and the crackle.
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = false;
            var noise = particles.noise;
            noise.enabled = false; // Short ballistic arcs, without accumulating sideways drift.
            var fade = particles.colorOverLifetime;
            var gradient = fade.color.gradient;
            gradient.alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.25f),
                new GradientAlphaKey(0f, 1f),
            };
            fade.color = gradient; // Visible at ignition, not first appearing high in the air.
            var collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.collidesWith = 1; // Static walls and props; render-only floors use GroundContact.
            collision.enableDynamicColliders = false;
            collision.radiusScale = 0.5f;
            collision.bounce = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            collision.dampen = 0.55f;
            collision.lifetimeLoss = 0.25f;
            collision.sendCollisionMessages = false;
            if (Application.isPlaying)
                fireAudio = particles.gameObject.AddComponent<ProceduralTorchFireAudio>();
        }
    }
}
