using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Hydra enemy that splits into smaller copies when killed.
    /// Each generation is smaller and weaker until minimum generation is reached.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public partial class HydraEnemyScript : EnemyBase
    {
        private const float MaxAttackLungeDistance = 0.42f;
        private const float MaxAttackPullBackDistance = 0.22f;

        [Header("Melee Attack")]
        [SerializeField]
        private float meleeRange = 0.9f;

        [SerializeField]
        private float meleeAttackCooldown = 0.5f;
        private float nextMeleeAttackTime = 0f;

        [Header("Attack Animation")]
        [SerializeField]
        private float attackWindupDuration = 0.28f;

        [SerializeField]
        private float attackStrikeDuration = 0.14f;

        [SerializeField]
        private float attackRecoverDuration = 0.26f;

        [SerializeField]
        private float attackPullBackDistance = 0.18f;

        [SerializeField]
        private float attackLungeDistance = 0.42f;

        [SerializeField]
        private Color attackFlashColor = Color.red;
        private bool isAttacking = false;
        private bool hasDamagedThisAttack = false;
        private float attackTimer = 0f;
        private int attackPhase = 0;
        private Vector3 attackStartPos;
        private Vector3 attackWindupPos;
        private Vector3 attackTargetPos;
        private Quaternion attackStartRotation;
        private Vector2 attackDirection;
        private float activeAttackReach;
        private Vector3 baseLocalScale;
        private SpriteRenderer spriteRenderer; // Local sprite renderer for attack animations
        private Color originalColor;
        private Transform visualTransform;
        private EnemyWalkAnimation walkAnimation;

        [Header("Melee Audio")]
        [SerializeField]
        private ProceduralEnemyMeleeAudio meleeAudio;

        protected override void Awake()
        {
            base.Awake();
            CaptureHydraSplitBaseline();
            walkAnimation = GetComponent<EnemyWalkAnimation>();

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
                visualRenderer = System.Array.Find(
                    GetComponentsInChildren<MeshRenderer>(true),
                    renderer => renderer.enabled
                );
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
                        $"[HydraEnemyScript] {name}: visualTransform '{visualTransform.name}' has zero scale! Using Vector3.one as fallback."
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
                // Never animate the Rigidbody/collider root.
                visualTransform = null;
                baseLocalScale = Vector3.one;
            }
        }

        protected override void FixedUpdate()
        {
            if (player == null)
                return;

            // Track the player while winding up, then commit and pin the body for
            // the released strike and recovery.
            if (isAttacking && attackPhase >= 2)
            {
                rb.SetGroundVelocity(Vector2.zero);
                EnemySpatialHash.Instance?.UpdatePosition(this);
                return;
            }

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
            AccelerateTowards(targetVel);

            base.FixedUpdate();
        }
    }
}
