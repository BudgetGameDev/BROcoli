using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// THE SINGLE SOURCE OF TRUTH FOR SPRAY DIRECTION.
    ///
    /// Hand tracks target, animates toward it, CurrentDirection is where we spray.
    /// No separate "target direction" vs "animated direction" - just ONE direction.
    /// </summary>
    public class SprayHandVisuals
    {
        private Transform handTransform;
        private SprayWeaponVisual3D weaponVisual;
        private Transform sprayTransform;
        private Transform playerTransform;
        private PlayerStats playerStats;
        private ParticleSystem[] sprayParticles;
        private readonly SprayWalkBob walkBob = new SprayWalkBob();
        private Vector2 previousPlayerPosition;
        private bool hasPreviousPlayerPosition;

        // Target tracking
        private Transform targetTransform;
        private Vector2? predictedTargetPosition = null;
        private float maxRange = 3f;

        // Animation state
        private float currentHandAngle = 0f;
        private float targetHandAngle = 0f;
        private float sprayPushBlend;
        private float sprayPushVelocity;
        private float sprayHoldUntil;

        public Transform HandTransform => handTransform;

        public SprayHandVisuals(Transform parent)
        {
            sprayTransform = parent;
            playerTransform = parent.parent;
            playerStats =
                playerTransform != null ? playerTransform.GetComponent<PlayerStats>() : null;
            if (playerTransform != null)
            {
                previousPlayerPosition = playerTransform.position.ToGround();
                hasPreviousPlayerPosition = true;
            }
        }

        public void CreateHandVisuals()
        {
            foreach (
                SpriteRenderer sprite in sprayTransform.GetComponentsInChildren<SpriteRenderer>(
                    true
                )
            )
                sprite.enabled = false;

            handTransform = sprayTransform.Find("SprayHand");
            if (handTransform == null)
            {
                GameObject handObject = new GameObject("SprayHand");
                handTransform = handObject.transform;
                handTransform.SetParent(sprayTransform, false);
            }

            handTransform.localPosition = new Vector3(SpraySettings.HandOffset, 0f, 0f);
            handTransform.localRotation = Quaternion.identity;
            weaponVisual = SprayWeaponVisual3D.Attach(handTransform.gameObject);
            sprayParticles = sprayTransform.GetComponentsInChildren<ParticleSystem>(true);
        }

        // ==================== TARGET TRACKING ====================

        public void SetTarget(Transform target)
        {
            targetTransform = target;
            predictedTargetPosition = null;
        }

        public void SetTarget(Transform target, Vector2 predictedPos)
        {
            targetTransform = target;
            predictedTargetPosition = predictedPos;
        }

        public void ClearTarget()
        {
            targetTransform = null;
            predictedTargetPosition = null;
        }

        public void SetRange(float range)
        {
            maxRange = range;
        }

        /// <summary>
        /// Get the center position of the current target (from collider bounds or transform)
        /// </summary>
        private Vector2 GetTargetCenter()
        {
            if (targetTransform == null)
                return Vector2.zero;
            Collider col = targetTransform.GetComponent<Collider>();
            return (col != null && col.enabled)
                ? col.bounds.center.ToGround()
                : targetTransform.position.ToGround();
        }

        public bool HasTarget =>
            targetTransform != null && targetTransform.gameObject.activeInHierarchy;

        public bool IsTargetInRange
        {
            get
            {
                if (targetTransform == null || playerTransform == null)
                    return false;
                // Measure distance from player center (consistent with aim calculation)
                Vector2 playerPos = playerTransform.position.ToGround();
                float dist = Vector2.Distance(playerPos, GetTargetCenter());
                return dist <= maxRange && dist >= SpraySettings.MinTargetDistance;
            }
        }

        // ==================== DIRECTION (THE ONLY DIRECTION) ====================

        /// <summary>
        /// THE spray direction. Where hand points RIGHT NOW. Particles and damage use THIS.
        /// </summary>
        public Vector2 CurrentDirection =>
            new Vector2(
                Mathf.Cos(currentHandAngle * Mathf.Deg2Rad),
                Mathf.Sin(currentHandAngle * Mathf.Deg2Rad)
            );

        public bool IsAimedAtTarget =>
            Mathf.Abs(Mathf.DeltaAngle(currentHandAngle, targetHandAngle))
            < SpraySettings.AngleToleranceForFiring;

        public Vector3 GetNozzleWorldPosition()
        {
            Transform nozzle = weaponVisual?.NozzleTransform;
            if (nozzle != null)
                return nozzle.position;

            Vector3 playerPos =
                playerTransform != null ? playerTransform.position : sprayTransform.position;
            Vector2 dir = CurrentDirection;
            float offset =
                SpraySettings.HandOffset
                + SpraySettings.NozzleLocalPos.x
                + sprayPushBlend * SpraySettings.HandSprayPushDistance;
            return new Vector3(
                playerPos.x + dir.x * offset,
                playerPos.y + SpraySettings.VisualHeightOffset,
                playerPos.z + dir.y * offset
            );
        }

        // ==================== UPDATE (CALL EVERY FRAME) ====================

        public void Update()
        {
            // Always track target (no freezing)
            if (
                targetTransform != null
                && playerTransform != null
                && targetTransform.gameObject.activeInHierarchy
            )
            {
                // Get target center (use predicted position if available)
                Vector2 targetPos = predictedTargetPosition ?? GetTargetCenter();

                // Calculate aim direction from PLAYER CENTER to target
                // This is geometrically correct: since nozzle is offset ALONG the aim ray,
                // aiming from player center ensures the spray ray passes through the target
                Vector2 playerPos = playerTransform.position.ToGround();
                Vector2 toTarget = targetPos - playerPos;

                if (toTarget.magnitude > 0.1f)
                    targetHandAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            }

            // Animate toward target
            float diff = Mathf.DeltaAngle(currentHandAngle, targetHandAngle);
            float maxRot = SpraySettings.HandRotationSpeed * Time.deltaTime;
            currentHandAngle += Mathf.Abs(diff) <= maxRot ? diff : Mathf.Sign(diff) * maxRot;

            if (currentHandAngle > 180f)
                currentHandAngle -= 360f;
            if (currentHandAngle < -180f)
                currentHandAngle += 360f;

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            // Rotating this parent moves the hand anchor around the player and yaws
            // the complete 3D hand/bottle assembly continuously. The model child must
            // inherit this rotation instead of cancelling it or flipping at screen left.
            sprayTransform.localRotation = GroundPlane.YawRotation(currentHandAngle);
            sprayTransform.localPosition = new Vector3(0, SpraySettings.VisualHeightOffset, 0);

            UpdateSprayPush();
            SprayWalkBob.Pose walkPose = walkBob.Update(ResolveMovementAmount(), Time.deltaTime);
            float hover =
                Mathf.Sin(Time.time * SpraySettings.HandHoverSpeed)
                * SpraySettings.HandHoverAmplitude;
            float push = sprayPushBlend * SpraySettings.HandSprayPushDistance;
            handTransform.localPosition =
                new Vector3(SpraySettings.HandOffset + push, hover, 0f) + walkPose.LocalOffset;

            float sprayTilt = -SpraySettings.HandSprayForwardTiltDegrees * sprayPushBlend;
            Vector3 presentation = walkPose.LocalEulerAngles;
            presentation.z += sprayTilt;
            weaponVisual?.SetPresentation(presentation);
        }

        private float ResolveMovementAmount()
        {
            if (playerTransform == null || Time.deltaTime <= 0f)
                return 0f;

            Vector2 currentPosition = playerTransform.position.ToGround();
            if (!hasPreviousPlayerPosition)
            {
                previousPlayerPosition = currentPosition;
                hasPreviousPlayerPosition = true;
                return 0f;
            }

            float distanceMoved = Vector2.Distance(currentPosition, previousPlayerPosition);
            previousPlayerPosition = currentPosition;
            float referenceSpeed = Mathf.Max(
                0.1f,
                playerStats != null ? playerStats.CurrentMovementSpeed : 4f
            );
            // Scene placement and teleports are not footsteps. A normal rendered-frame
            // displacement is a small fraction of this even at boosted movement speed.
            if (distanceMoved > referenceSpeed * 0.5f)
                return 0f;

            float measuredSpeed = distanceMoved / Time.deltaTime;
            return Mathf.Clamp01(measuredSpeed / referenceSpeed);
        }

        private void UpdateSprayPush()
        {
            bool particlesActive = false;
            if (sprayParticles != null)
            {
                foreach (ParticleSystem particles in sprayParticles)
                {
                    if (
                        particles != null
                        && particles.gameObject.activeInHierarchy
                        && (particles.isEmitting || particles.particleCount > 0)
                    )
                    {
                        particlesActive = true;
                        break;
                    }
                }
            }

            if (particlesActive)
                sprayHoldUntil = Time.time + SpraySettings.HandSprayHoldAfterEmission;

            bool pushForward = particlesActive || Time.time < sprayHoldUntil;
            float smoothTime = pushForward
                ? SpraySettings.HandSprayPushInTime
                : SpraySettings.HandSprayReturnTime;
            sprayPushBlend = Mathf.SmoothDamp(
                sprayPushBlend,
                pushForward ? 1f : 0f,
                ref sprayPushVelocity,
                smoothTime
            );
        }

        public void SetVisible(bool visible)
        {
            weaponVisual?.SetVisible(visible);
        }

        public bool HasHand => handTransform != null;
    }
}
