using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class HydraEnemyScript
    {
        private const int HydraUnlockRing = 4;
        private const int RingsPerHydraLevel = 4;
        private const int MaximumExtraSplitGenerations = 2;
        private const float SizeDrivenSpeedExponent = 0.55f;

        [Header("Hydra Split Settings")]
        [SerializeField]
        private int currentGeneration;

        [SerializeField]
        private int maxGenerations = 2;

        [SerializeField]
        private float childScaleMultiplier = 0.7f;

        [SerializeField]
        private float childHealthMultiplier = 0.5f;

        [SerializeField]
        private float childDamageMultiplier = 0.7f;

        [SerializeField]
        [Tooltip("Minimum speed increase per split. Smaller children may gain more speed.")]
        private float childSpeedMultiplier = 1.1f;

        [SerializeField]
        private float splitSpawnRadius = 0.5f;

        [SerializeField]
        private float splitImpulse = 3f;

        public event Action<HydraEnemyScript> OnChildSpawned;

        private static bool isQuitting;
        private bool hasSpawnedChildren;
        private int baseMaxGenerations;
        private float baseMeleeRange;
        private Vector3 hydraBaseLocalScale;
        private int hydraLevel = 1;
        private bool splitBaselineCaptured;

        public int CurrentGeneration => currentGeneration;
        public int SplitGenerations => maxGenerations;
        public int HydraLevel => hydraLevel;

        private void CaptureHydraSplitBaseline()
        {
            baseMaxGenerations = Mathf.Max(0, maxGenerations);
            baseMeleeRange = meleeRange;
            hydraBaseLocalScale = transform.localScale;
            splitBaselineCaptured = true;
        }

        private void EnsureHydraSplitBaseline()
        {
            if (!splitBaselineCaptured)
                CaptureHydraSplitBaseline();
        }

        /// <summary>Promotes a root hydra as dungeon distance increases.</summary>
        public void ConfigureForDungeonRing(int ring)
        {
            EnsureHydraSplitBaseline();
            int extraGenerations = ExtraSplitGenerationsForRing(ring);
            hydraLevel = extraGenerations + 1;
            currentGeneration = 0;
            maxGenerations = baseMaxGenerations + extraGenerations;
            transform.localScale =
                hydraBaseLocalScale
                * RootScaleMultiplierForExtraSplits(extraGenerations, childScaleMultiplier);
            meleeRange = baseMeleeRange;
        }

        public static int ExtraSplitGenerationsForRing(int ring)
        {
            int ringsBeyondUnlock = Mathf.Max(0, ring - HydraUnlockRing);
            return Mathf.Clamp(
                ringsBeyondUnlock / RingsPerHydraLevel,
                0,
                MaximumExtraSplitGenerations
            );
        }

        public static float RootScaleMultiplierForExtraSplits(
            int extraGenerations,
            float scalePerSplit
        )
        {
            float safeScale = Mathf.Clamp(scalePerSplit, 0.1f, 1f);
            return Mathf.Pow(1f / safeScale, Mathf.Max(0, extraGenerations));
        }

        public static float ChildSpeedForScale(
            float parentSpeed,
            float scalePerSplit,
            float minimumMultiplier
        )
        {
            float safeScale = Mathf.Clamp(scalePerSplit, 0.1f, 1f);
            float sizeDrivenMultiplier = Mathf.Pow(1f / safeScale, SizeDrivenSpeedExponent);
            return parentSpeed * Mathf.Max(1f, minimumMultiplier, sizeDrivenMultiplier);
        }

        public void InitAsChild(
            int generation,
            int inheritedMaxGenerations,
            int inheritedHydraLevel,
            float parentHealth,
            float parentDamage,
            float parentSpeed,
            float parentMeleeRange,
            Vector3 parentScale
        )
        {
            EnsureHydraSplitBaseline();
            currentGeneration = generation;
            maxGenerations = inheritedMaxGenerations;
            hydraLevel = inheritedHydraLevel;
            OnChildSpawned = null;
            hasSpawnedChildren = false;

            MaxHealth = parentHealth * childHealthMultiplier;
            Health = MaxHealth;
            Damage = parentDamage * childDamageMultiplier;
            Speed = ChildSpeedForScale(parentSpeed, childScaleMultiplier, childSpeedMultiplier);
            transform.localScale = parentScale * childScaleMultiplier;
            ScoreValue = Mathf.Max(10, ScoreValue / 2);
            meleeRange = parentMeleeRange * childScaleMultiplier;
        }

        public override void Die()
        {
            if (isQuitting || !gameObject.scene.isLoaded)
                return;

            if (!hasSpawnedChildren && currentGeneration < maxGenerations)
                SpawnChildren();

            base.Die();
        }

        private void SpawnChildren() =>
            SpawnChildren(
                spawnPosition =>
                    PoolManager.Instance?.GetEnemy(this, spawnPosition, Quaternion.identity),
                InstantiateChild
            );

        internal GameObject InstantiateChild(Vector3 spawnPosition) =>
            Instantiate(gameObject, spawnPosition, Quaternion.identity);

        internal void SpawnChildren(
            System.Func<Vector3, EnemyBase> getPooledEnemy,
            System.Func<Vector3, GameObject> instantiateEnemy
        )
        {
            hasSpawnedChildren = true;

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
            GameplayDiagnostics.Record("combat.hydra-split");
#endif
            const int childrenToSpawn = 2;
            for (int i = 0; i < childrenToSpawn; i++)
            {
                float angle = (360f / childrenToSpawn) * i + Random.Range(-15f, 15f);
                Vector2 offset =
                    new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad))
                    * splitSpawnRadius;
                Vector3 spawnPosition = transform.position + offset.ToWorld();

                EnemyBase pooledEnemy = getPooledEnemy(spawnPosition);
                HydraEnemyScript childHydra = pooledEnemy as HydraEnemyScript;
                if (childHydra != null)
                {
                    childHydra.SetPooled(true);
                }
                else
                {
                    GameObject child = instantiateEnemy(spawnPosition);
                    childHydra = child.GetComponent<HydraEnemyScript>();
                }

                if (childHydra == null)
                    continue;

                childHydra.InitAsChild(
                    currentGeneration + 1,
                    maxGenerations,
                    hydraLevel,
                    MaxHealth,
                    Damage,
                    Speed,
                    meleeRange,
                    transform.localScale
                );
                OnChildSpawned?.Invoke(childHydra);

                if (childHydra.rb != null)
                    childHydra.rb.SetGroundVelocity(offset.normalized * splitImpulse);
            }
        }

        public override void ResetForPool()
        {
            EnsureHydraSplitBaseline();
            base.ResetForPool();

            hasSpawnedChildren = false;
            OnChildSpawned = null;
            currentGeneration = 0;
            maxGenerations = baseMaxGenerations;
            hydraLevel = 1;
            meleeRange = baseMeleeRange;
            transform.localScale = hydraBaseLocalScale;
            isAttacking = false;
            hasDamagedThisAttack = false;
            attackPhase = 0;
            attackTimer = 0f;
            nextMeleeAttackTime = 0f;
            attackDirection = Vector2.zero;
            activeAttackReach = 0f;
            walkAnimation?.SetAttackOverride(false);

            if (visualTransform != null)
                visualTransform.localScale = baseLocalScale;

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = originalColor;
            }
        }

        void OnApplicationQuit()
        {
            isQuitting = true;
        }
    }
}
