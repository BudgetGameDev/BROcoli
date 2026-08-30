using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Produces a smooth, looping local pose for the complete sanitizer hand/bottle rig.
    /// The phase only advances while moving, so stopping settles the current step instead
    /// of snapping to a time-driven pose when movement starts again.
    /// </summary>
    public sealed class SprayWalkBob
    {
        public readonly struct Pose
        {
            public Pose(Vector3 localOffset, Vector3 localEulerAngles)
            {
                LocalOffset = localOffset;
                LocalEulerAngles = localEulerAngles;
            }

            public Vector3 LocalOffset { get; }
            public Vector3 LocalEulerAngles { get; }
        }

        private float phase;
        private float movementBlend;

        public float MovementBlend => movementBlend;

        public Pose Update(float movementAmount, float deltaTime)
        {
            float target = Mathf.Clamp01(movementAmount);
            float blendSpeed =
                target > movementBlend
                    ? SpraySettings.HandWalkBlendInSpeed
                    : SpraySettings.HandWalkBlendOutSpeed;
            movementBlend = Mathf.MoveTowards(
                movementBlend,
                target,
                Mathf.Max(0f, deltaTime) * blendSpeed
            );

            if (target > 0.001f && deltaTime > 0f)
            {
                float pace = Mathf.Lerp(0.75f, 1.15f, target);
                phase = Mathf.Repeat(
                    phase + deltaTime * SpraySettings.HandWalkWobbleSpeed * pace,
                    Mathf.PI * 2f
                );
            }

            return Evaluate(phase, movementBlend);
        }

        public static Pose Evaluate(float walkPhase, float blend)
        {
            float amount = Mathf.Clamp01(blend);
            float step = Mathf.Sin(walkPhase);
            float doubleStep = Mathf.Cos(walkPhase * 2f);

            Vector3 offset =
                new Vector3(
                    doubleStep * SpraySettings.HandWalkForwardBobDistance,
                    -doubleStep * SpraySettings.HandWalkBobDistance,
                    step * SpraySettings.HandWalkSwayDistance
                ) * amount;

            Vector3 rotation =
                new Vector3(
                    Mathf.Sin(walkPhase * 2f + 0.4f) * SpraySettings.HandWalkPitchDegrees,
                    -step * SpraySettings.HandWalkYawDegrees,
                    -step * SpraySettings.HandWalkWobbleDegrees
                ) * amount;
            return new Pose(offset, rotation);
        }
    }
}
