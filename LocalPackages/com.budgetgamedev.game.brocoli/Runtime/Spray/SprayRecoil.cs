using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Produces a short recoil kick and recovery arc whenever the sanitizer starts firing.
    /// This pose is additive so it can layer over the weapon's hover and walk animation.
    /// </summary>
    public sealed class SprayRecoil
    {
        public readonly struct Pose
        {
            public Pose(Vector3 localOffset, Vector3 localEulerAngles, float influence)
            {
                LocalOffset = localOffset;
                LocalEulerAngles = localEulerAngles;
                Influence = influence;
            }

            public Vector3 LocalOffset { get; }
            public Vector3 LocalEulerAngles { get; }
            public float Influence { get; }
        }

        private float elapsedTime;
        private bool isActive;

        public bool IsActive => isActive;

        public static float PeakNormalizedTime =>
            Mathf.Clamp01(
                SpraySettings.HandSprayRecoilAttackTime
                    / Mathf.Max(0.001f, SpraySettings.HandSprayRecoilDuration)
            );

        public void Trigger()
        {
            elapsedTime = 0f;
            isActive = true;
        }

        public Pose Update(float deltaTime)
        {
            if (!isActive)
                return Evaluate(1f);

            float duration = Mathf.Max(0.001f, SpraySettings.HandSprayRecoilDuration);
            elapsedTime = Mathf.Min(duration, elapsedTime + Mathf.Max(0f, deltaTime));
            Pose pose = Evaluate(elapsedTime / duration);
            if (elapsedTime >= duration)
                isActive = false;
            return pose;
        }

        public static Pose Evaluate(float normalizedTime)
        {
            float progress = Mathf.Clamp01(normalizedTime);
            if (progress >= 1f)
                return new Pose(Vector3.zero, Vector3.zero, 0f);

            float peakTime = Mathf.Max(0.001f, PeakNormalizedTime);
            float influence;
            if (progress < peakTime)
                influence = Mathf.SmoothStep(0f, 1f, progress / peakTime);
            else
            {
                float recovery = (progress - peakTime) / Mathf.Max(0.001f, 1f - peakTime);
                influence = 1f - Mathf.SmoothStep(0f, 1f, recovery);
            }

            float lift = Mathf.Sin(progress * Mathf.PI) * influence;

            return new Pose(
                new Vector3(
                    -SpraySettings.HandSprayRecoilDistance * influence,
                    SpraySettings.HandSprayRecoilLift * lift,
                    0f
                ),
                new Vector3(0f, 0f, SpraySettings.HandSprayRecoilDegrees * influence),
                influence
            );
        }

        public static float ResolveWalkPoseWeight(float recoilInfluence)
        {
            return Mathf.Lerp(
                1f,
                SpraySettings.HandWalkWeightDuringRecoil,
                Mathf.Clamp01(recoilInfluence)
            );
        }

        public static float ResolveBackpedalAmount(
            Vector2 movementDelta,
            Vector2 aimDirection,
            float movementAmount
        )
        {
            if (
                movementDelta.sqrMagnitude <= 0.000001f
                || aimDirection.sqrMagnitude <= 0.000001f
            )
                return 0f;

            float opposingAim = Mathf.Clamp01(
                -Vector2.Dot(movementDelta.normalized, aimDirection.normalized)
            );
            return opposingAim * Mathf.Clamp01(movementAmount);
        }

        public static float ResolveMovementMultiplier(float movementBlend, float backpedalBlend)
        {
            float movingMultiplier = Mathf.Lerp(
                1f,
                SpraySettings.HandMovingRecoilMultiplier,
                Mathf.Clamp01(movementBlend)
            );
            float backpedalMultiplier = Mathf.Lerp(
                1f,
                SpraySettings.HandBackpedalRecoilMultiplier,
                Mathf.Clamp01(backpedalBlend)
            );
            return movingMultiplier * backpedalMultiplier;
        }
    }
}
