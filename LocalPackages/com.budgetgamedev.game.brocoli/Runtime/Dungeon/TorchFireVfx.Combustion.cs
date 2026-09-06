using System;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed partial class TorchFireVfx
    {
        private ParticleSystem emberParticles;
        private ProceduralTorchFireAudio fireAudio;
        private System.Random combustionRandom;
        private float nextCrackle;

        private void Update()
        {
            if (AudioListener.pause || Time.timeScale <= 0f)
                return;
            Transform player = GameContext.Instance?.PlayerTransform;
            if (
                player != null
                && GroundPlane.GroundDistance(transform.position, player.position) > 18f
            )
                return;
            AdvanceCombustion(Time.deltaTime);
        }

        internal void AdvanceCombustion(float deltaTime, Action<float> onCrackle = null)
        {
            if (emberParticles == null || !isActiveAndEnabled || deltaTime <= 0f)
                return;
            nextCrackle -= deltaTime;
            if (nextCrackle > 0f)
                return;
            EmitCrackle(RandomCombustion(0.25f, 1f), onCrackle);
            // No catch-up loop: unpausing or returning to a torch never replays a backlog.
            nextCrackle = RandomCombustion(0.4f, 1.25f);
        }

        internal int EmitCrackle(float strength, Action<float> onCrackle = null)
        {
            if (emberParticles == null || !isActiveAndEnabled)
                return 0;
            strength = float.IsNaN(strength) ? 0f : Mathf.Clamp01(strength);
            int count =
                strength > 0.8f ? 3
                : strength > 0.45f ? 2
                : 1;
            count = Mathf.Min(
                count,
                emberParticles.main.maxParticles - emberParticles.particleCount
            );
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < count; i++)
            {
                // Emit in world space from a small disk inside the lower flame. There is
                // no cone volume or prewarm that could spawn old sparks away from the fuel.
                float angle = RandomCombustion(0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(RandomCombustion(0f, 1f)) * 0.045f;
                Vector3 position =
                    emberParticles.transform.position
                    + Vector3.up * 0.035f
                    + right * (Mathf.Cos(angle) * radius)
                    + forward * (Mathf.Sin(angle) * radius + 0.025f);
                var particle = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity =
                        Vector3.up * RandomCombustion(0.9f, 1.65f)
                        + forward * RandomCombustion(0.08f, 0.22f)
                        + right * RandomCombustion(-0.10f, 0.10f),
                    applyShapeToPosition = false,
                    randomSeed = (uint)combustionRandom.Next(1, int.MaxValue),
                };
                emberParticles.Emit(particle, 1);
            }
            if (count > 0)
            {
                fireAudio?.PlayCrackle(strength);
                onCrackle?.Invoke(strength);
            }
            return count;
        }

        private float RandomCombustion(float min, float max) =>
            Mathf.Lerp(min, max, (float)combustionRandom.NextDouble());

        private void OnDisable()
        {
            if (emberParticles != null)
                emberParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (fireAudio != null)
                fireAudio.enabled = false;
        }

        private void OnEnable()
        {
            if (emberParticles != null && Application.isPlaying)
                emberParticles.Play();
            if (fireAudio != null)
                fireAudio.enabled = true;
        }
    }
}
