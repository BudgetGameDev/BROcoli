using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyProjectile : MonoBehaviour
    {
        public float damage = 10f;
        public float speed = 10f;
        public float lifeTime = 5f;

        [Header("Fizzle Effect")]
        [SerializeField]
        private float fizzleStartTime = 3f; // When to start fizzling (seconds before death)

        [SerializeField]
        private Transform visualTransform; // The 3D model to scale during fizzle

        [SerializeField]
        private float spinSpeed = 180f; // Rotation speed in degrees/second

        private Rigidbody rb;
        private Collider col;
        private float spawnTime;
        private Vector3 initialScale;
        private Vector2 travelDirection;

        // Pooling support
        private bool _isPooled = false;
        private bool _isSpent;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            col.isTrigger = true;

            if (visualTransform != null)
                initialScale = visualTransform.localScale;
        }

        public void Init(Vector2 direction)
        {
            travelDirection = direction.normalized;
            rb.SetGroundVelocity(travelDirection * speed * PlayerStats.ActiveEnemyTimeScale);
            spawnTime = Time.time;
            _isSpent = false;

            // Capture initial scale if not already done
            if (visualTransform != null && initialScale == Vector3.zero)
                initialScale = visualTransform.localScale;

            // A recycled shot inherits the shrunken scale its fizzle left behind,
            // so undo that before it is seen again.
            if (visualTransform != null)
                visualTransform.localScale = initialScale;

            if (col != null)
                col.enabled = true;

            // For non-pooled objects, use Destroy with timer
            if (!_isPooled)
            {
                Destroy(gameObject, lifeTime);
            }
        }

        /// <summary>
        /// Mark this projectile as pooled (affects lifetime handling).
        /// </summary>
        public void SetPooled(bool pooled)
        {
            _isPooled = pooled;
        }

        void Update()
        {
            // Check lifetime for pooled objects
            if (_isPooled && Time.time - spawnTime > lifeTime)
            {
                Despawn();
                return;
            }

            if (rb != null && travelDirection != Vector2.zero)
            {
                rb.SetGroundVelocity(travelDirection * speed * PlayerStats.ActiveEnemyTimeScale);
            }

            // Spin the visual
            if (visualTransform != null)
            {
                visualTransform.Rotate(
                    0f,
                    spinSpeed * PlayerStats.ActiveEnemyTimeScale * Time.deltaTime,
                    0f,
                    Space.Self
                );
            }

            // Fizzle out effect - shrink towards end of life
            float timeAlive = Time.time - spawnTime;
            float fizzleThreshold = lifeTime - fizzleStartTime;

            if (timeAlive > fizzleThreshold && visualTransform != null)
            {
                float fizzleProgress = (timeAlive - fizzleThreshold) / fizzleStartTime;
                float scale = Mathf.Lerp(1f, 0f, fizzleProgress);
                visualTransform.localScale = initialScale * scale;
            }
        }

        void FixedUpdate()
        {
            if (travelDirection == Vector2.zero)
                return;

            Vector3 displacement = (
                travelDirection * speed * PlayerStats.ActiveEnemyTimeScale * Time.fixedDeltaTime
            ).ToWorld();
            if (!ProjectileWallCollision.Sweep(col, transform, displacement, out _))
                return;

            DespawnOnWall();
        }

        void OnTriggerEnter(Collider other)
        {
            if (_isSpent)
                return;

            // Check if hit player
            if (other.CompareTag("Player"))
            {
                if (DamagePlayer(other, damage))
                    ProceduralEnemyProjectileHitAudio.PlayHit(
                        transform.position,
                        ProceduralEnemyProjectileHitAudio.EnemyHitSoundType.PlasmaImpact,
                        0.45f
                    );

                Despawn();
            }
            // Despawn on hitting walls/obstacles (but not other enemies)
            else if (!other.CompareTag("Enemy") && !other.isTrigger)
            {
                DespawnOnWall();
            }
        }

        internal static bool DamagePlayer(Collider player, float amount)
        {
            var handler = player.GetComponentInParent<PlayerDamageHandler>(true);
            if (handler != null)
                return handler.TakeProjectileDamage(amount);
            // Isolated targets without a controller still receive damage (e.g. test dummies).
            var stats = player.GetComponentInChildren<PlayerStats>();
            if (stats == null || !stats.IsAlive)
                return false;
            stats.ApplyDamage(amount);
            return true;
        }

        private void DespawnOnWall()
        {
            travelDirection = Vector2.zero;
            if (rb != null)
                rb.linearVelocity = Vector3.zero;
            if (col != null)
                col.enabled = false;
            Despawn();
        }

        /// <summary>
        /// Retires a spent shot: pooled ones go back for reuse, loose ones are
        /// destroyed. Guarded so a second trigger in the same frame cannot return
        /// the same projectile to its pool twice.
        /// </summary>
        private void Despawn()
        {
            if (_isSpent)
                return;
            _isSpent = true;

            if (_isPooled)
                PoolManager.Instance?.ReturnProjectile(this);
            else
                Destroy(gameObject);
        }
    }
}
