using UnityEngine;

namespace BudgetGameDev.Autoplay
{
    /// <summary>
    /// Works out how much game time one rendered frame may advance.
    ///
    /// The fast-forward is "fake time": <see cref="Time.captureDeltaTime"/> makes the
    /// engine advance a fixed amount of game time per frame and render as fast as it
    /// can, so wall-clock time compresses while the simulation keeps its own clock.
    /// Physics is unaffected -- it still runs at the fixed step, sub-stepped as many
    /// times per frame as the capture step covers.
    ///
    /// That sub-stepping is the limit. Unity refuses to run more than
    /// <see cref="Time.maximumDeltaTime"/> of physics per frame, so a capture step
    /// far above the fixed step makes physics silently fall behind the game clock and
    /// the run stops testing the game as it ships. Clamping here keeps a bigger
    /// <c>--timestep</c> honest instead of quietly wrong.
    /// </summary>
    public static class AutoplayTimeControl
    {
        /// <summary>An explicit readiness wait advances neither simulation nor its measurements.</summary>
        public static bool WaitingForReadiness { get; set; }

        /// <summary>Below this a frame advances so little that nothing is gained.</summary>
        public const float MinimumStep = 1f / 240f;

        /// <summary>Physics sub-steps one rendered frame may cover.</summary>
        public const int MaximumPhysicsStepsPerFrame = 4;

        public static float ResolveCaptureStep(float requested, float fixedStep)
        {
            float ceiling = Mathf.Max(MinimumStep, fixedStep * MaximumPhysicsStepsPerFrame);
            return Mathf.Clamp(requested, MinimumStep, ceiling);
        }

        /// <summary>
        /// Game-seconds this frame advanced. Under the fake-time fast-forward that is
        /// the capture step, not the wall-clock frame time -- measuring a run with
        /// <see cref="Time.unscaledDeltaTime"/> counts real seconds, which silently
        /// turns "simulate three game-hours" into "sit here for three hours" and
        /// makes the reported speedup 1x by construction.
        /// </summary>
        public static float GameDelta =>
            WaitingForReadiness ? 0f
            : Time.captureDeltaTime > 0f ? Time.captureDeltaTime
            : Time.unscaledDeltaTime;

        /// <summary>Game-seconds simulated per wall-clock second; 0 before any elapse.</summary>
        public static float Speedup(float gameSeconds, float realSeconds) =>
            realSeconds > 0.0001f ? gameSeconds / realSeconds : 0f;
    }
}
