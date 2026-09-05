using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class ExpGain : MonoBehaviour
    {
        public enum DropStyle
        {
            Enemy,
            Chest,
        }

        private const float EnemyDropHeight = 0.65f;
        private const float EnemyDropDuration = 0.34f;
        private const float ChestDropHeight = 2.2f;
        private const float ChestDropDuration = 0.72f;
        private const float LandingSettleDuration = 0.08f;

        public int expAmountGain;
        private Rigidbody rb;
        private Collider col;
        private PickupVisual3D _pickupVisual;
        private float _currentSpeed = 0f;
        private bool _localAttractionLocked;
        private bool _isCollected;
        private bool _isDropping;
        private bool _isCollectible;
        private Vector3 _dropStart;
        private Vector3 _dropLandingPosition;
        private float _dropHeight;
        private float _dropDuration;
        private float _dropElapsed;
        private float _landingSettleRemaining;

        // Pooling support. Orbs never expire: dropped experience waits on the floor
        // until the player walks over it, however long that run takes.
        private bool _isPooled = false;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            _pickupVisual = PickupVisual3D.AttachExperience(gameObject);
            rb.useGravity = false;
            rb.constraints =
                RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            col.isTrigger = true;
        }

        void OnEnable()
        {
            _isCollected = false;
            _isDropping = false;
            _isCollectible = true;
            _landingSettleRemaining = 0f;
            PickupAttraction.Reset(
                rb,
                ref _currentSpeed,
                ref _localAttractionLocked,
                _pickupVisual
            );
        }

        public void Init(int expAmount)
        {
            expAmountGain = expAmount;
            RestoreGroundedPhysics();
            _isDropping = false;
            _isCollectible = true;
            _landingSettleRemaining = 0f;
            PickupAttraction.Reset(
                rb,
                ref _currentSpeed,
                ref _localAttractionLocked,
                _pickupVisual
            );
        }

        /// <summary>
        /// Initializes an orb and launches it toward its resting point. Attraction and
        /// collection remain disabled until the complete arc and landing settle finish.
        /// </summary>
        public void InitDropped(int expAmount, Vector3 landingPosition, DropStyle style)
        {
            Init(expAmount);

            float height = style == DropStyle.Chest ? ChestDropHeight : EnemyDropHeight;
            float duration = style == DropStyle.Chest ? ChestDropDuration : EnemyDropDuration;
            BeginDrop(landingPosition, height, duration);

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
            GameplayDiagnostics.Record("pickup.experience-dropped");
#endif
        }

        /// <summary>Returns a point along a parabolic pickup drop arc.</summary>
        public static Vector3 DropArcPosition(
            Vector3 start,
            Vector3 landingPosition,
            float height,
            float normalizedTime
        )
        {
            float t = Mathf.Clamp01(normalizedTime);
            Vector3 position = Vector3.Lerp(start, landingPosition, t);
            position.y += Mathf.Max(0f, height) * 4f * t * (1f - t);
            return position;
        }

        /// <summary>
        /// Mark this ExpGain as pooled (affects how collection recycles it).
        /// </summary>
        public void SetPooled(bool pooled)
        {
            _isPooled = pooled;
        }

        void FixedUpdate()
        {
            if (_isDropping)
            {
                AdvanceDrop();
                return;
            }

            if (!_isCollectible)
            {
                _landingSettleRemaining -= Time.fixedDeltaTime;
                if (_landingSettleRemaining <= 0f)
                    UnlockAfterLanding();
                return;
            }

            PickupAttraction.UpdateMotion(
                rb,
                ref _currentSpeed,
                ref _localAttractionLocked,
                _pickupVisual
            );
        }

        void OnTriggerEnter(Collider other)
        {
            TryCollect(other);
        }

        private void TryCollect(Collider other)
        {
            if (_isCollected || !_isCollectible)
                return;

            PlayerStats stats = other.GetComponentInParent<PlayerStats>();
            if (stats == null)
                return;

            _isCollected = true;

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
            GameplayDiagnostics.Record("pickup.experience");
#endif
            int experience = expAmountGain;

            // Hide/recycle before applying XP. Applying XP can pause the game for
            // a level-up choice, so the orb must already be gone at that point.
            if (_isPooled)
                ReturnToPool();
            else
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }

            ProceduralXPPickupAudio.PlayPickup();
            stats.ApplyExperience(experience);
        }

        private void BeginDrop(Vector3 landingPosition, float height, float duration)
        {
            _dropStart = rb != null ? rb.position : transform.position;
            _dropLandingPosition = landingPosition;
            _dropHeight = Mathf.Max(0f, height);
            _dropDuration = Mathf.Max(Time.fixedDeltaTime, duration);
            _dropElapsed = 0f;
            _landingSettleRemaining = LandingSettleDuration;
            _isDropping = true;
            _isCollectible = false;

            if (col != null)
                col.enabled = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.isKinematic = true;
            }
        }

        private void AdvanceDrop()
        {
            _dropElapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(_dropElapsed / _dropDuration);
            Vector3 position = DropArcPosition(
                _dropStart,
                _dropLandingPosition,
                _dropHeight,
                progress
            );

            if (rb != null)
                rb.MovePosition(position);
            else
                transform.position = position;

            if (progress < 1f)
                return;

            _isDropping = false;
            if (rb != null)
                rb.position = _dropLandingPosition;
            else
                transform.position = _dropLandingPosition;
        }

        private void UnlockAfterLanding()
        {
            RestoreGroundedPhysics();
            _isCollectible = true;
            if (col != null)
                col.enabled = true;
        }

        private void RestoreGroundedPhysics()
        {
            if (rb == null)
                return;

            rb.isKinematic = false;
            rb.constraints =
                RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        }

        private void ReturnToPool()
        {
            PoolManager.Instance?.ReturnExpGain(this);
        }
    }
}
