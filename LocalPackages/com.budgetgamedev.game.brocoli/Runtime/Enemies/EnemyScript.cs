using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public partial class EnemyScript : EnemyBase
    {
        private const float MaxAttackLungeDistance = 0.42f;
        private const float MaxAttackPullBackDistance = 0.22f;

        [Header("Melee Attack")]
        [SerializeField]
        private float meleeRange = 0.25f; // Distance to trigger attack (very close range)

        [SerializeField]
        private float meleeAttackCooldown = 0.6f; // Time between attacks
        private float nextMeleeAttackTime = 0f;

        [Header("Attack Animation")]
        [SerializeField]
        private float attackWindupDuration = 0.28f; // Longer anticipation before striking

        [SerializeField]
        private float attackStrikeDuration = 0.14f; // Fast release after the charge

        [SerializeField]
        private float attackRecoverDuration = 0.26f;

        [SerializeField]
        private float attackPullBackDistance = 0.18f;

        [SerializeField]
        private float attackLungeDistance = 0.42f;

        [SerializeField]
        private Color attackFlashColor = Color.red; // Color flash on attack
        private bool isAttacking = false;
        private bool hasDamagedThisAttack = false; // Prevents double-damage per attack
        private float attackTimer = 0f;
        private int attackPhase = 0; // 0=idle, 1=windup, 2=strike, 3=recover
        private Vector3 attackStartPos;
        private Vector3 attackWindupPos;
        private Vector3 attackTargetPos;
        private Quaternion attackStartRotation;
        private Vector2 attackDirection;
        private float activeAttackReach;
        private Vector3 baseLocalScale;
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private Transform visualTransform;
        private EnemyWalkAnimation walkAnimation;

        [Header("Melee Audio")]
        [SerializeField]
        private ProceduralEnemyMeleeAudio meleeAudio;

        protected override void Awake()
        {
            base.Awake();
            walkAnimation = GetComponent<EnemyWalkAnimation>();

            // Try to get melee audio component if not assigned
            if (meleeAudio == null)
                meleeAudio = GetComponent<ProceduralEnemyMeleeAudio>();

            // Find visual transform for attack animation
            // Enemy prefabs use FBX models with MeshRenderer, not SpriteRenderer
            Renderer visualRenderer = null;

            // First check for enabled SpriteRenderer
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.enabled)
                {
                    spriteRenderer = sr;
                    visualRenderer = sr;
                    break;
                }
            }

            // If no enabled SpriteRenderer, find MeshRenderer (FBX models)
            if (visualRenderer == null)
            {
                foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.enabled)
                    {
                        visualRenderer = mr;
                        break;
                    }
                }
            }

            // Set up visual transform from the renderer we found
            if (visualRenderer != null && visualRenderer.transform != transform)
            {
                visualTransform = visualRenderer.transform;
                baseLocalScale = visualTransform.localScale;

                // Safety check: if scale is zero (shouldn't happen), use a reasonable default
                if (baseLocalScale.sqrMagnitude < 0.0001f)
                {
                    Debug.LogWarning(
                        $"[EnemyScript] {name}: visualTransform '{visualTransform.name}' has zero scale! Using Vector3.one as fallback."
                    );
                    baseLocalScale = Vector3.one;
                    visualTransform.localScale = Vector3.one;
                }

                if (spriteRenderer != null)
                {
                    originalColor = spriteRenderer.color;
                }
            }
            else
            {
                // Never animate the Rigidbody/collider root. Color feedback can
                // still play, but moving or scaling this transform would move the
                // solid body directly through the player.
                visualTransform = null;
                baseLocalScale = Vector3.one;
            }
        }

        protected override void FixedUpdate()
        {
            if (player == null)
                return;

            // Keep pursuing and aiming during the telegraphed windup. Once the
            // strike releases, pin the body so the visual lunge cannot cause
            // physics jitter or shove the player.
            if (isAttacking && attackPhase >= 2)
            {
                // Update spatial hash but don't move or apply separation during attack
                EnemySpatialHash.Instance?.UpdatePosition(this);
                // Zero out velocity during attack to prevent drift
                rb.SetGroundVelocity(Vector2.zero);
                return;
            }

            // Don't move toward player during the brief, rate-limited hit recoil.
            if (isKnockedBack)
            {
                base.FixedUpdate();
                return;
            }

            Vector2 dir = player.position.ToGround() - rb.GroundPosition();
            float distToPlayer = dir.magnitude;

            if (distToPlayer < 0.0001f)
                return;

            dir.Normalize();

            float colliderGap = GetPlayerColliderGap();
            float standOffGap = Mathf.Max(0f, playerStandOffGap);
            const float standOffDeadZone = 0.025f;
            Vector2 targetVel;

            if (colliderGap > standOffGap + standOffDeadZone)
            {
                targetVel = dir * Speed * EnemyTimeScale;
            }
            else if (colliderGap < standOffGap - standOffDeadZone)
            {
                float retreatSpeed = Mathf.Min(
                    Speed,
                    Mathf.Max(0.35f, (standOffGap - colliderGap) * acceleration)
                );
                targetVel = -dir * retreatSpeed * EnemyTimeScale;
            }
            else
            {
                targetVel = Vector2.zero;
            }

            // Smooth acceleration towards target velocity
            AccelerateTowards(targetVel);

            // Apply separation AFTER movement (so it can push away)
            base.FixedUpdate();
        }
    }
}
