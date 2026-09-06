using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed partial class TorchFireVfx
    {
        private DungeonManager emberDungeon;
        private readonly ParticleSystem.Particle[] groundParticles = new ParticleSystem.Particle[
            12
        ];

        private void LateUpdate()
        {
            if (
                emberParticles == null
                || emberDungeon == null
                || Time.timeScale <= 0f
                || AudioListener.pause
            )
                return;
            int count = emberParticles.GetParticles(groundParticles);
            bool changed = false;
            for (int i = 0; i < count; i++)
                changed |= ResolveEmberGroundContact(ref groundParticles[i], emberDungeon.Layout);
            if (changed)
                emberParticles.SetParticles(groundParticles, count);
        }

        internal static bool ResolveEmberGroundContact(
            ref ParticleSystem.Particle particle,
            DungeonLayout layout
        )
        {
            // Floors are render meshes used for navigation, not physics colliders.
            // Only the actual playable platform supplies this y=0 contact surface;
            // sparks outside its cliff edge must continue falling into the void.
            if (
                layout == null
                || particle.position.y > 0.015f
                || particle.velocity.y >= 0f
                || !layout.IsPlayableRoom(DungeonLayout.RoomAt(particle.position.ToGround()))
            )
                return false;
            Vector3 position = particle.position;
            position.y = 0.015f;
            particle.position = position;
            Vector3 velocity = particle.velocity;
            particle.velocity = new Vector3(
                velocity.x * 0.55f,
                -velocity.y * 0.18f,
                velocity.z * 0.55f
            );
            particle.remainingLifetime = Mathf.Min(particle.remainingLifetime, 0.22f);
            return true;
        }
    }
}
