using UnityEngine;

namespace BudgetGameDev.Shared
{
    public partial class iOSSafariWebGLOptimizer
    {
        /// <summary>
        /// The quality knobs the optimizer turns down, carried as values rather than
        /// as writes. QualitySettings is project state an open Editor owns and
        /// flushes to ProjectSettings/QualitySettings.asset when it quits, so the
        /// policy is stated here and applied in one place.
        /// </summary>
        internal readonly struct QualitySnapshot
        {
            internal QualitySnapshot(
                int antiAliasing,
                bool softParticles,
                bool softVegetation,
                bool billboardsFaceCameraPosition,
                float lodBias,
                int maximumLodLevel,
                int particleRaycastBudget
            )
            {
                AntiAliasing = antiAliasing;
                SoftParticles = softParticles;
                SoftVegetation = softVegetation;
                BillboardsFaceCameraPosition = billboardsFaceCameraPosition;
                LodBias = lodBias;
                MaximumLodLevel = maximumLodLevel;
                ParticleRaycastBudget = particleRaycastBudget;
            }

            internal int AntiAliasing { get; }
            internal bool SoftParticles { get; }
            internal bool SoftVegetation { get; }
            internal bool BillboardsFaceCameraPosition { get; }
            internal float LodBias { get; }
            internal int MaximumLodLevel { get; }
            internal int ParticleRaycastBudget { get; }

            /// <summary>
            /// What iOS Safari is given: no MSAA and cheaper detail, with the pixel
            /// light budget and the quality level deliberately left alone.
            /// </summary>
            internal static QualitySnapshot LightingSafe =>
                new(0, false, false, false, 0.5f, 2, 16);

            /// <summary>What the project is set to right now.</summary>
            internal static QualitySnapshot Current =>
                new(
                    QualitySettings.antiAliasing,
                    QualitySettings.softParticles,
                    QualitySettings.softVegetation,
                    QualitySettings.billboardsFaceCameraPosition,
                    QualitySettings.lodBias,
                    QualitySettings.maximumLODLevel,
                    QualitySettings.particleRaycastBudget
                );

            internal void ApplyToProject()
            {
                QualitySettings.antiAliasing = AntiAliasing;
                QualitySettings.softParticles = SoftParticles;
                QualitySettings.softVegetation = SoftVegetation;
                QualitySettings.billboardsFaceCameraPosition = BillboardsFaceCameraPosition;
                QualitySettings.lodBias = LodBias;
                QualitySettings.maximumLODLevel = MaximumLodLevel;
                QualitySettings.particleRaycastBudget = ParticleRaycastBudget;
            }

            public override string ToString() =>
                $"msaa {AntiAliasing}, soft particles {SoftParticles}, soft vegetation "
                + $"{SoftVegetation}, billboards {BillboardsFaceCameraPosition}, lod bias "
                + $"{LodBias}, max lod {MaximumLodLevel}, raycast budget {ParticleRaycastBudget}";
        }
    }
}
