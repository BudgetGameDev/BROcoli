using UnityEngine;

public class ShuffleWalkVisual : MonoBehaviour
{
    public PlayerController controller;

    // Timing
    private const float MaxChargeTime = 0.125f;
    private const float MinChargeTime = 0.03f;  // Minimum 30ms charge - no canceling
    private const float JumpTime = 0.5f;
    private const float BhopGroundTime = 0.06f;
    private const float StoppingTime = 0.35f;
    
    // Heights - reduced for lower jumps
    private const float MinJumpHeight = 0.1f;
    private const float MaxJumpHeight = 0.45f;
    private const float MinChargeDip = 0.03f;
    private const float MaxChargeDip = 0.15f;
    
    // Power (0-1 multiplier on movement)
    private const float MinJumpPower = 0.3f;
    private const float MaxJumpPower = 1f;
    
    // Squash/stretch
    private const float ChargeSquash = 0.3f;
    private const float AirStretch = 0.15f;
    private const float LandSquash = 0.25f;
    
    // Idle animation
    private const float IdleBreathSpeed = 0.8f;
    private const float IdleSwayMaxAngle = 6f;
    private const float IdleSwaySpeed = 2.5f;
    
    // Bhop variation - organic feel through landing quality
    private const float BhopTwistMax = 5f;  // Reduced - subtle rotation only
    
    private const float DeadZone = 0.05f;

    Vector3 startLocalPos;
    Vector3 startScale;
    
    public enum HopState { Idle, Charging, Airborne, BhopBounce, Landing, Stopping }
    public HopState State { get; private set; } = HopState.Idle;
    
    float stateTimer;
    float displayHeight;
    float displaySS;
    Vector3 displayScale;
    
    Vector2 committedDirection;
    float currentPower;
    float currentJumpHeight;
    float currentJumpTime;  // Varies per jump
    float inputMagnitude;   // How far the stick is pushed (0-1)
    float launchInputMagnitude; // Locked at launch time
    bool releasedDuringCharge;  // Track if player released during charge
    
    // Stopping momentum
    Vector2 stoppingVelocity;
    
    // Idle animation
    float idleTime;
    float idleSwayTarget;
    float idleSwayAngle;
    float idleSwayTimer;
    
    // Bhop variation - "landing quality" system
    float landingQuality;     // 0 = rough landing, 1 = perfect landing
    float bhopTwistTarget;
    float bhopTwistAngle;
    float currentBounceTime;  // Varies based on landing quality
    float leanMultiplier = 1f;
    
    // Output for other scripts
    public float IdleLeanAngle => idleSwayAngle;
    public float BhopTwistAngle => bhopTwistAngle;
    public float LeanMultiplier => leanMultiplier;
    
    // Smoothed output for PlayerController
    Vector2 smoothedMovement;

    public Vector2 MovementDirection => smoothedMovement;
    public bool IsMoving => State == HopState.Airborne || State == HopState.BhopBounce;

    void Awake()
    {
        startLocalPos = transform.localPosition;
        startScale = transform.localScale;
        displayScale = startScale;
    }

    void Update()
    {
        if (!controller) return;

        float dt = Time.deltaTime;
        idleTime += dt;
        
        Vector2 input = controller.RawInput;
        if (input.sqrMagnitude > 1f) input.Normalize();
        
        // Store input magnitude for scaling hops (0-1)
        inputMagnitude = Mathf.Clamp01(input.magnitude);
        
        bool wantsToMove = input.sqrMagnitude >= DeadZone * DeadZone;
        
        float targetHeight = 0f;
        float targetSS = 0f;
        Vector2 targetMovement = Vector2.zero;
        
        switch (State)
        {
            case HopState.Idle:
                // Side-to-side weight shifting sway
                idleSwayTimer -= dt;
                if (idleSwayTimer <= 0f)
                {
                    // Pick new random lean angle (left or right)
                    idleSwayTarget = Random.Range(-IdleSwayMaxAngle, IdleSwayMaxAngle);
                    idleSwayTimer = Random.Range(1.2f, 2.5f);
                }
                idleSwayAngle = Mathf.Lerp(idleSwayAngle, idleSwayTarget, IdleSwaySpeed * dt);
                leanMultiplier = 0f;  // No lean when idle
                
                // Subtle breathing in scale only, no height change
                float breathPhase = idleTime * IdleBreathSpeed * Mathf.PI * 2f;
                float breath = (Mathf.Sin(breathPhase) + 1f) * 0.5f;
                
                targetHeight = 0f; // No vertical movement
                targetSS = breath * 0.015f; // Very subtle scale pulse
                
                if (wantsToMove)
                {
                    State = HopState.Charging;
                    stateTimer = 0f;
                    currentPower = 0f;
                    committedDirection = input.normalized;
                    releasedDuringCharge = false;
                    idleSwayAngle = 0f; // Reset sway when starting to move
                }
                targetMovement = Vector2.zero;
                break;
                
            case HopState.Charging:
                stateTimer += dt;
                float chargeT = Mathf.Clamp01(stateTimer / MaxChargeTime);
                
                currentPower = Mathf.Lerp(MinJumpPower, MaxJumpPower, chargeT);
                
                float currentDip = Mathf.Lerp(MinChargeDip, MaxChargeDip, chargeT);
                targetHeight = -currentDip;
                targetSS = -ChargeSquash * chargeT;
                leanMultiplier = -chargeT * 0.3f;  // Slight lean back while charging
                
                targetMovement = Vector2.zero;  // LOCKED in place during charge
                
                // Update direction while still holding
                if (wantsToMove)
                    committedDirection = input.normalized;
                else
                    releasedDuringCharge = true;  // Mark that player released
                
                bool maxCharged = chargeT >= 1f;
                bool minChargeReached = stateTimer >= MinChargeTime;
                
                // Auto-launch when: max charged OR min charge reached (if released or still holding)
                // Once you tap, you're committed - no canceling
                if (maxCharged || (minChargeReached && releasedDuringCharge))
                {
                    LaunchJump();
                }
                break;
                
            case HopState.Airborne:
                stateTimer += dt;
                float jumpT = Mathf.Clamp01(stateTimer / currentJumpTime);
                
                float parabola = 4f * jumpT * (1f - jumpT);
                targetHeight = currentJumpHeight * parabola;
                targetSS = AirStretch * parabola * launchInputMagnitude;
                
                // Lean dynamics: slight lean back at edges, forward at peak
                leanMultiplier = Mathf.Sin(jumpT * Mathf.PI) * 0.6f;
                
                // Subtle twist during air
                bhopTwistAngle = Mathf.Lerp(bhopTwistAngle, bhopTwistTarget, 4f * dt);
                
                // Air strafing - can adjust direction mid-air
                if (wantsToMove)
                {
                    committedDirection = Vector2.Lerp(committedDirection, input.normalized, 8f * dt);
                }
                
                // Movement during airborne - carry momentum
                targetMovement = committedDirection * currentPower * launchInputMagnitude;
                
                if (jumpT >= 1f)
                {
                    // Determine landing quality for next bounce
                    landingQuality = Random.Range(0.5f, 1f);
                    currentBounceTime = Mathf.Lerp(0.1f, 0.05f, landingQuality);
                    
                    if (wantsToMove)
                    {
                        // Continue bhopping - go to quick bounce
                        State = HopState.BhopBounce;
                        stateTimer = 0f;
                    }
                    else
                    {
                        // Stop - go to stopping animation with momentum
                        State = HopState.Stopping;
                        stateTimer = 0f;
                        stoppingVelocity = committedDirection * currentPower * launchInputMagnitude;
                    }
                }
                break;
                
            case HopState.BhopBounce:
                stateTimer += dt;
                float bounceT = Mathf.Clamp01(stateTimer / currentBounceTime);
                
                // Quick bounce animation
                float bounceDown = Mathf.Sin(bounceT * Mathf.PI);
                float dipAmount = Mathf.Lerp(0.8f, 0.3f, landingQuality);
                targetHeight = -MaxChargeDip * dipAmount * bounceDown;
                targetSS = -LandSquash * dipAmount * bounceDown;
                
                // Lean back on landing
                float leanBackAmount = Mathf.Lerp(0.3f, 0.1f, landingQuality);
                leanMultiplier = -leanBackAmount * bounceDown;
                
                // CARRY MOMENTUM during bhop bounce
                targetMovement = committedDirection * currentPower * launchInputMagnitude;
                
                // Build up power while bhopping if holding direction
                if (wantsToMove)
                {
                    currentPower = Mathf.MoveTowards(currentPower, MaxJumpPower, (MaxJumpPower - MinJumpPower) * 2f * dt);
                    committedDirection = Vector2.Lerp(committedDirection, input.normalized, 15f * dt);
                }
                
                if (bounceT >= 1f)
                {
                    if (wantsToMove)
                    {
                        // Chain into next jump - use built up power
                        launchInputMagnitude = Mathf.Max(launchInputMagnitude, 0.8f); // Keep momentum high
                        currentJumpHeight = Mathf.Lerp(MinJumpHeight, MaxJumpHeight, currentPower);
                        currentJumpHeight *= landingQuality * Random.Range(0.9f, 1.1f);
                        currentJumpTime = JumpTime * Mathf.Lerp(0.8f, 1.1f, landingQuality);
                        bhopTwistTarget = Random.Range(-BhopTwistMax, BhopTwistMax);
                        
                        State = HopState.Airborne;
                        stateTimer = 0f;
                    }
                    else
                    {
                        State = HopState.Stopping;
                        stateTimer = 0f;
                        stoppingVelocity = committedDirection * currentPower * launchInputMagnitude;
                    }
                }
                break;
                
            case HopState.Landing:
                stateTimer += dt;
                float landT = Mathf.Clamp01(stateTimer / 0.1f);  // 100ms landing
                
                // Landing squash animation
                float landSquash = Mathf.Sin(landT * Mathf.PI);
                targetHeight = -MinChargeDip * landSquash;
                targetSS = -LandSquash * 0.5f * landSquash;
                leanMultiplier = 0f;
                
                // NO movement during full stop landing
                targetMovement = Vector2.zero;
                
                if (landT >= 1f)
                {
                    currentPower = 0f;
                    if (wantsToMove)
                    {
                        State = HopState.Charging;
                        stateTimer = 0f;
                        committedDirection = input.normalized;
                        releasedDuringCharge = false;
                    }
                    else
                    {
                        State = HopState.Idle;
                        stateTimer = 0f;
                        idleSwayTimer = 0f;
                        idleSwayAngle = 0f;
                    }
                }
                break;
                
            case HopState.Stopping:
                stateTimer += dt;
                float stopT = Mathf.Clamp01(stateTimer / StoppingTime);
                
                // Three phase stop: lean forward (skid), lean back (catch), settle
                float leanIntensity = stoppingVelocity.magnitude;
                
                if (stopT < 0.35f)
                {
                    // Phase 1: Lean into momentum (skidding forward)
                    float p = stopT / 0.35f;
                    float leanFwd = Mathf.Sin(p * Mathf.PI * 0.5f);
                    targetHeight = -MaxChargeDip * leanFwd * leanIntensity;
                    targetSS = -0.18f * leanFwd * leanIntensity;
                    targetMovement = stoppingVelocity * (1f - p * 0.7f);
                    leanMultiplier = leanFwd * leanIntensity;  // Lean forward while skidding
                }
                else if (stopT < 0.7f)
                {
                    // Phase 2: Lean back (catching balance)
                    float p = (stopT - 0.35f) / 0.35f;
                    float leanBack = Mathf.Sin(p * Mathf.PI);
                    targetHeight = MinChargeDip * leanBack * leanIntensity * 0.5f;
                    targetSS = 0.08f * leanBack * leanIntensity;
                    targetMovement = stoppingVelocity * 0.3f * (1f - p);
                    leanMultiplier = -leanBack * leanIntensity * 0.5f;  // Lean back catching balance
                }
                else
                {
                    // Phase 3: Settle to idle
                    float p = (stopT - 0.7f) / 0.3f;
                    targetHeight = Mathf.Lerp(MinChargeDip * 0.2f, 0f, p);
                    targetSS = Mathf.Lerp(0.02f, 0f, p);
                    targetMovement = Vector2.zero;
                    leanMultiplier = Mathf.Lerp(-0.15f, 0f, p);  // Settle to neutral
                }
                
                if (stopT >= 1f)
                {
                    currentPower = 0f;
                    if (wantsToMove)
                    {
                        State = HopState.Charging;
                        stateTimer = 0f;
                        committedDirection = input.normalized;
                        releasedDuringCharge = false;
                    }
                    else
                    {
                        State = HopState.Idle;
                        stateTimer = 0f;
                        idleSwayTimer = 0f;
                        idleSwayAngle = 0f;
                    }
                }
                else if (wantsToMove)
                {
                    // Can interrupt stopping to start moving again
                    State = HopState.Charging;
                    stateTimer = 0f;
                    currentPower = 0f;
                    committedDirection = input.normalized;
                    releasedDuringCharge = false;
                }
                break;
        }

        // Movement output - NO smoothing for tight physics sync
        smoothedMovement = targetMovement;
        
        // Visual smoothing
        displayHeight = Mathf.Lerp(displayHeight, targetHeight, 25f * dt);
        
        Vector3 localOffset = transform.parent != null 
            ? transform.parent.InverseTransformDirection(Vector3.up * displayHeight)
            : Vector3.up * displayHeight;
        transform.localPosition = startLocalPos + localOffset;
        
        displaySS = Mathf.Lerp(displaySS, targetSS, 25f * dt);
        float yScale = 1f + displaySS;
        float xzScale = 1f - displaySS * 0.5f;
        
        displayScale.x = Mathf.Lerp(displayScale.x, startScale.x * xzScale, 25f * dt);
        displayScale.y = Mathf.Lerp(displayScale.y, startScale.y * yScale, 25f * dt);
        displayScale.z = Mathf.Lerp(displayScale.z, startScale.z * xzScale, 25f * dt);
        transform.localScale = displayScale;
    }
    
    void LaunchJump()
    {
        State = HopState.Airborne;
        stateTimer = 0f;
        
        // Lock input magnitude at launch - no changes during flight
        launchInputMagnitude = Mathf.Max(inputMagnitude, 0.5f);  // Minimum 50% power on tap
        
        // Scale jump height and power by input magnitude (how far stick is pushed)
        float scaledPower = currentPower * launchInputMagnitude;
        currentJumpHeight = Mathf.Lerp(MinJumpHeight, MaxJumpHeight, (scaledPower - MinJumpPower) / (MaxJumpPower - MinJumpPower));
        currentJumpHeight *= launchInputMagnitude; // Further scale height
        currentJumpTime = Mathf.Lerp(JumpTime * 0.6f, JumpTime, launchInputMagnitude); // Shorter hops when input is low
        bhopTwistTarget = Random.Range(-BhopTwistMax, BhopTwistMax);
    }
}
