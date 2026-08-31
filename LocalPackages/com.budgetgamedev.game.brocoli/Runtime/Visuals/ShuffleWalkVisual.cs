using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ShuffleWalkVisual : MonoBehaviour
    {
        public PlayerController controller;

        // Reference to player stats for speed-based animation scaling
        private PlayerStats _playerStats;
        private const float BaseMovementSpeed = 4f; // Default speed - animation tuned for this

        // Timing (base values - scaled by speed)
        private const float MaxChargeTime = 0.125f;
        private const float MinChargeTime = 0.03f; // Minimum 30ms charge - no canceling
        private const float BaseJumpTime = 0.5f; // Base jump time - scales with speed
        private const float BhopGroundTime = 0.06f;
        private const float StoppingTime = 0.35f;

        // Heights - scaled by speed for bigger hops at higher speeds
        private const float BaseMinJumpHeight = 0.1f;
        private const float BaseMaxJumpHeight = 0.45f;
        private const float MinChargeDip = 0.03f;
        private const float MaxChargeDip = 0.15f;

        // Power (0-1 multiplier on movement)
        private const float MinJumpPower = 0.3f;
        private const float MaxJumpPower = 1f;

        // Speed scaling limits
        private const float MinSpeedMultiplier = 0.7f; // Floor for slow speeds
        private const float MaxSpeedMultiplier = 1.6f; // Cap for fast speeds

        // Squash/stretch
        private const float ChargeSquash = 0.3f;
        private const float AirStretch = 0.15f;
        private const float LandSquash = 0.25f;

        // Idle animation
        private const float IdleBreathSpeed = 0.8f;
        private const float IdleSwayMaxAngle = 6f;
        private const float IdleSwaySpeed = 2.5f;

        // Bhop variation - organic feel through landing quality
        private const float BhopTwistMax = 5f; // Reduced - subtle rotation only

        // The virtual controller already applies its radial dead zone. This only
        // rejects floating-point residue from other input sources.
        private const float DeadZone = 0.01f;
        private const float WallVisualSkin = 0.02f;
        private const float WallAnimationClearance = 0.3f;
        private const float VerticalHopFallbackScale = 0.9f;

        // Stumble system - slows player after being hit
        private const float StumbleSpeedMultiplier = 0.5f; // 50% speed when stumbling
        private float stumblePenalty = 0f; // 0 = no stumble, 1 = full stumble

        Vector3 startLocalPos;
        Vector3 startScale;
        Collider playerCollider;
        int wallLayerMask;
        readonly RaycastHit[] wallHits = new RaycastHit[8];

        public enum HopState
        {
            Idle,
            Charging,
            Airborne,
            BhopBounce,
            Landing,
            Stopping,
        }

        public HopState State { get; private set; } = HopState.Idle;

        float stateTimer;

        float displayHeight;
        float displaySS;
        Vector3 displayScale;

        Vector2 committedDirection;
        float currentPower;
        float currentJumpHeight;
        float currentJumpTime; // Varies per jump
        float inputMagnitude; // How far the stick is pushed (0-1)
        float launchInputMagnitude; // Locked at launch time
        bool releasedDuringCharge; // Track if player released during charge

        // Stopping momentum
        Vector2 stoppingVelocity;

        // Idle animation
        float idleTime;
        float idleSwayTarget;
        float idleSwayAngle;
        float idleSwayTimer;

        // Bhop variation - "landing quality" system
        float landingQuality; // 0 = rough landing, 1 = perfect landing
        float bhopTwistTarget;
        float bhopTwistAngle;
        float currentBounceTime; // Varies based on landing quality
        float leanMultiplier = 1f;
        float wallPoseFactor = 1f;

        // Output for other scripts
        public float IdleLeanAngle => idleSwayAngle;
        public float BhopTwistAngle => bhopTwistAngle;
        public float LeanMultiplier => leanMultiplier * wallPoseFactor;
        public float WallPoseFactor => wallPoseFactor;

        // Smoothed output for PlayerController
        Vector2 smoothedMovement;

        public Vector2 MovementDirection => smoothedMovement;
        public bool IsMoving => State == HopState.Airborne || State == HopState.BhopBounce;

        void Awake()
        {
            startLocalPos = transform.localPosition;
            startScale = transform.localScale;
            displayScale = startScale;

            // Discover PlayerStats
            if (controller != null)
            {
                _playerStats = controller.GetComponent<PlayerStats>();
                playerCollider = controller.GetComponent<Collider>();
            }

            if (playerCollider == null)
                playerCollider = GetComponentInParent<Collider>();

            wallLayerMask = LayerMask.GetMask("Wall");
        }

        /// <summary>
        /// Calculate speed multiplier for animation scaling.
        /// Higher speed = taller jumps, faster timing.
        /// </summary>
        private float GetSpeedMultiplier()
        {
            if (_playerStats == null)
            {
                // Try to discover if we don't have it
                if (controller != null)
                    _playerStats = controller.GetComponent<PlayerStats>();
                if (_playerStats == null)
                    return 1f;
            }

            float currentSpeed = _playerStats.CurrentMovementSpeed;
            float ratio = currentSpeed / BaseMovementSpeed;
            return Mathf.Clamp(ratio, MinSpeedMultiplier, MaxSpeedMultiplier);
        }

        /// <summary>
        /// Get current jump time scaled by speed - faster movement = faster hops.
        /// </summary>
        private float GetScaledJumpTime()
        {
            float speedMult = GetSpeedMultiplier();
            // Faster speed = shorter jump time (inverse relationship)
            return BaseJumpTime / speedMult;
        }

        /// <summary>
        /// Get current min jump height scaled by speed - faster = higher hops.
        /// </summary>
        private float GetScaledMinJumpHeight()
        {
            float speedMult = GetSpeedMultiplier();
            return BaseMinJumpHeight * speedMult;
        }

        /// <summary>
        /// Get current max jump height scaled by speed - faster = higher hops.
        /// </summary>
        private float GetScaledMaxJumpHeight()
        {
            float speedMult = GetSpeedMultiplier();
            return BaseMaxJumpHeight * speedMult;
        }

        private struct FrameOutput
        {
            public float Height;
            public float SquashStretch;
            public Vector2 Movement;
        }

        void Update()
        {
            if (!controller)
                return;

            float dt = Time.deltaTime;
            idleTime += dt;

            Vector2 input = controller.RawInput;
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            inputMagnitude = Mathf.Clamp01(input.magnitude);
            bool wantsToMove = input.sqrMagnitude >= DeadZone * DeadZone;
            FrameOutput output = default;

            switch (State)
            {
                case HopState.Idle:
                    UpdateIdle(dt, input, wantsToMove, ref output);
                    break;
                case HopState.Charging:
                    UpdateCharging(dt, input, wantsToMove, ref output);
                    break;
                case HopState.Airborne:
                    UpdateAirborne(dt, input, wantsToMove, ref output);
                    break;
                case HopState.BhopBounce:
                    UpdateBhopBounce(dt, input, wantsToMove, ref output);
                    break;
                case HopState.Landing:
                    UpdateLanding(dt, input, wantsToMove, ref output);
                    break;
                case HopState.Stopping:
                    UpdateStopping(dt, input, wantsToMove, ref output);
                    break;
            }

            smoothedMovement = output.Movement;
            ApplyPresentation(dt, output);
        }

        private void ApplyPresentation(float dt, FrameOutput output)
        {
            displayHeight = Mathf.Lerp(displayHeight, output.Height, 25f * dt);

            // Presentation cheat, not a real jump: the hop displaces along ground-
            // north, which the fixed chase camera reads as screen-up (pre-flip +Y).
            // Keep visual-only displacement inside the player's physics footprint.
            float visibleHopHeight = ClampHopOffsetAgainstWalls(displayHeight);
            Vector2 poseDirection =
                output.Movement.sqrMagnitude > DeadZone * DeadZone
                    ? output.Movement.normalized
                    : committedDirection;
            wallPoseFactor = GetWallPoseFactor(poseDirection);

            // Preserve the bounce in world-up when the ground-north offset is blocked.
            float blockedPositiveHop = Mathf.Max(0f, displayHeight - visibleHopHeight);
            Vector3 hopOffset =
                Vector3.forward * visibleHopHeight
                + Vector3.up * (blockedPositiveHop * VerticalHopFallbackScale);
            transform.localPosition =
                startLocalPos + transform.parent.InverseTransformDirection(hopOffset);

            displaySS = Mathf.Lerp(displaySS, output.SquashStretch, 25f * dt);
            float visibleSS = displaySS * wallPoseFactor;
            float stretch = 1f + visibleSS;
            float squash = 1f - visibleSS * 0.5f;

            displayScale.x = Mathf.Lerp(displayScale.x, startScale.x * squash, 25f * dt);
            displayScale.y = Mathf.Lerp(displayScale.y, startScale.y * squash, 25f * dt);
            displayScale.z = Mathf.Lerp(displayScale.z, startScale.z * stretch, 25f * dt);
            transform.localScale = displayScale;
        }
    }
}
