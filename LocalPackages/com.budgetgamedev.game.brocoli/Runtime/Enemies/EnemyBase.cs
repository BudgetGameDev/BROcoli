using System;
using System.Collections;
using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public abstract partial class EnemyBase : MonoBehaviour
    {
        private const float MaxSafeSeparationRadius = 1.15f;
        private const float MaxSafeSeparationForce = 8f;
        private const float MaxSafeSeparationSpeed = 2f;
        private const float MaxSafeKnockbackForce = 3f;
        private const float MaxSafeKnockbackDuration = 0.16f;
        private const float MinSafeKnockbackCooldown = 0.2f;
        private static PhysicsMaterial sharedEnemyBodyMaterial;

        [SerializeField]
        Bar healthBar;

        [SerializeField]
        protected ExpGain expGainPrefab;

        public event Action<EnemyBase> OnDeath;

        public bool healthBarVisable = false;
        public bool alwaysShowHealthBar = false;
        public float TimeToStartSpawning = 0f;
        public float TimeToEndSpawning = 60f;
        public int ScoreValue = 100;
        public float Damage = 0f;

        private static bool isQuitting = false;

        [Header("Enemy Stats")]
        public float Speed = 2f;
        public float Health = 50f;
        public float MaxHealth = 50f;

        [Header("Elite Settings")]
        public bool isElite = false;
        public float eliteHealthMultiplier = 3f;
        public float eliteScaleMultiplier = 1.3f;

        public event Action<Vector3> OnEliteDeath;

        [Header("Physics Tuning")]
        public float acceleration = 25f;

        [Header("Separation")]
        [SerializeField]
        protected float separationRadius = 1.15f; // Roughly one enemy body diameter

        [SerializeField]
        protected float separationForce = 8f; // Gentle steering, never an impulse

        [SerializeField]
        protected float playerSeparationRadius = 0.4f; // Minimum distance from player (reduced for close melee)

        [SerializeField]
        protected float playerSeparationForce = 8f; // How hard to avoid player overlap (gentle push)

        [SerializeField]
        protected float maxSeparationSpeed = 2f; // Separation should never look like knockback

        [Tooltip("Collider-to-collider gap melee enemies maintain from the player.")]
        [SerializeField]
        protected float playerStandOffGap = 0.4f;

        [Header("Walk Audio (Optional)")]
        [SerializeField]
        protected ProceduralEnemyWalkAudio walkAudio;

        public Transform player;

        public Rigidbody rb;
        protected float EnemyTimeScale => PlayerStats.ActiveEnemyTimeScale;
        protected Collider bodyCollider;
        private Collider playerCollider;

        protected SpriteRenderer cachedSpriteRenderer;
        protected Color originalSpriteColor;
        protected MeshRenderer cachedMeshRenderer;
        protected Color originalMeshColor;
        private MaterialPropertyBlock meshColorProperties;
        private bool _isPooled = false;
        private bool attackBodyLocked = false;
        private RigidbodyConstraints constraintsBeforeAttack;
        private bool hasQueuedKnockback;
        private Vector2 queuedKnockbackDirection;
        private float queuedKnockbackForce;
        private Vector3 baseLocalScale;
        private float baseHealth;
        private float baseMaxHealth;
        private float baseSpeed;
        private float baseDamage;
        private int baseScoreValue;
        private Quaternion baseLocalRotation;

        [Header("Death Animation")]
        [Tooltip("Randomized squash-and-pop duration keeps groups from disappearing in lockstep.")]
        [SerializeField]
        private Vector2 deathAnimationDurationRange = new Vector2(0.32f, 0.42f);

        [Tooltip("How much of the death is spent on the bright anticipation squash.")]
        [SerializeField, Range(0.1f, 0.4f)]
        private float deathAnticipationFraction = 0.24f;

        [Tooltip("Local scale multiplier at the end of the anticipation squash.")]
        [SerializeField]
        private Vector3 deathSquashScale = new Vector3(1.18f, 0.72f, 1.18f);

        [Tooltip("Fraction of deaths that add a small randomized spin while shrinking.")]
        [SerializeField, Range(0f, 1f)]
        private float deathSpinChance = 0.6f;

        [SerializeField]
        private Vector2 deathSpinDegreesRange = new Vector2(35f, 110f);
        private bool isDying;

        public bool IsDying => isDying;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();
            EnforceSafePhysicsLimits();

            ConfigureSolidBody();

            baseLocalScale = transform.localScale;
            baseLocalRotation = transform.localRotation;
            baseHealth = Health;
            baseMaxHealth = MaxHealth;
            baseSpeed = Speed;
            baseDamage = Damage;
            baseScoreValue = ScoreValue;

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.enabled)
                {
                    cachedSpriteRenderer = sr;
                    break;
                }
            }

            if (cachedSpriteRenderer == null)
            {
                foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.enabled)
                    {
                        cachedMeshRenderer = mr;
                        meshColorProperties = new MaterialPropertyBlock();
                        originalMeshColor = EnemyRendererColor.Get(mr, meshColorProperties);
                        break;
                    }
                }
            }
            else
            {
                originalSpriteColor = cachedSpriteRenderer.color;
            }

            if (walkAudio == null)
                walkAudio = GetComponent<ProceduralEnemyWalkAudio>();

            DisableWorldHealthBar();
        }

        /// <summary>Make this enemy an elite variant with increased HP and visual effects.</summary>
        public void MakeElite()
        {
            if (isElite)
                return;

            isElite = true;
            Health *= eliteHealthMultiplier;
            MaxHealth *= eliteHealthMultiplier;
            ScoreValue = Mathf.RoundToInt(ScoreValue * 2.5f);
            transform.localScale *= eliteScaleMultiplier;

            var effects = GetComponent<EliteEnemyEffects>();
            if (effects == null)
                effects = gameObject.AddComponent<EliteEnemyEffects>();
            effects.ApplyEliteVisuals();

            alwaysShowHealthBar = true;
        }

        protected virtual void OnEnable()
        {
            // OnEnable also runs after an editor script reload. Clamp live enemies
            EnforceSafePhysicsLimits();
            ConfigureSolidBody();

            var context = GameContext.Instance;
            if (context != null)
            {
                player = context.PlayerTransform;
            }

            playerCollider = FindSolidCollider(player);

            if (rb != null)
            {
                rb.linearDamping = 3f;
            }

            EnemySpatialHash.Instance?.Register(this);
            DisableWorldHealthBar();
            DiabloHud.EnsurePresent();
        }

        private void EnforceSafePhysicsLimits()
        {
            separationRadius = Mathf.Clamp(separationRadius, 0f, MaxSafeSeparationRadius);
            separationForce = Mathf.Clamp(separationForce, 0f, MaxSafeSeparationForce);
            maxSeparationSpeed = Mathf.Clamp(maxSeparationSpeed, 0f, MaxSafeSeparationSpeed);
            playerStandOffGap = Mathf.Clamp(playerStandOffGap, 0f, 0.6f);
            enemyKnockbackForce = Mathf.Clamp(enemyKnockbackForce, 0f, MaxSafeKnockbackForce);
            minimumEnemyKnockbackForce = Mathf.Clamp(
                minimumEnemyKnockbackForce,
                0f,
                enemyKnockbackForce
            );
            minimumDamageFractionForKnockback = Mathf.Clamp01(minimumDamageFractionForKnockback);
            damageFractionForMaxKnockback = Mathf.Clamp(damageFractionForMaxKnockback, 0.01f, 1f);
            rareMaximumKnockbackChance = Mathf.Clamp(rareMaximumKnockbackChance, 0f, 0.2f);
            knockbackRandomBias = Mathf.Clamp(knockbackRandomBias, 1f, 8f);
            enemyKnockbackDuration = Mathf.Clamp(
                enemyKnockbackDuration,
                0f,
                MaxSafeKnockbackDuration
            );
            enemyKnockbackCooldown = Mathf.Max(enemyKnockbackCooldown, MinSafeKnockbackCooldown);
        }

        private void ConfigureSolidBody()
        {
            if (bodyCollider == null)
                bodyCollider = GetComponent<Collider>();

            // Enemy body colliders are for navigation/blocking. Combat damage is
            // validated separately by the attack animation at its impact frame.
            if (bodyCollider != null)
            {
                bodyCollider.isTrigger = false;

                // Solid crowd contacts should slide, never bounce or grip and
                // convert tangential movement into a shove.
                if (sharedEnemyBodyMaterial == null)
                {
                    sharedEnemyBodyMaterial = new PhysicsMaterial("Enemy Body - No Bounce")
                    {
                        dynamicFriction = 0f,
                        staticFriction = 0f,
                        bounciness = 0f,
                        frictionCombine = PhysicsMaterialCombine.Minimum,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                }
                bodyCollider.sharedMaterial = sharedEnemyBodyMaterial;
            }

            // Enemy bodies must collide with one another so they cannot overlap.
            // Explicitly restore this because older play sessions may still have
            // the Enemy/Enemy layer pair disabled by the previous workaround.
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0 && Physics.GetIgnoreLayerCollision(enemyLayer, enemyLayer))
                Physics.IgnoreLayerCollision(enemyLayer, enemyLayer, false);
        }
    }
}
