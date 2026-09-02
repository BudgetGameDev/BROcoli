using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// A treasure chest placed in dungeon rooms. Walking into it bursts open a
    /// scatter of experience pickups plus one or more weighted boost powerups,
    /// then the chest pops and vanishes.
    /// </summary>
    [DisallowMultipleComponent]
    public class LootChest : MonoBehaviour
    {
        [SerializeField, Min(0)]
        private int expDropCount = 5;

        [SerializeField, Min(1)]
        [Tooltip("XP in each orb in the first dungeon ring. Matches the earliest enemy reward.")]
        private int experiencePerPickup = 10;

        [SerializeField, Min(0f)]
        [Tooltip("Additional XP per orb for each dungeon ring beyond the first.")]
        private float experienceGrowthPerRing = 0.5f;

        [SerializeField, Min(0)]
        private int boostDropCount = 1;

        [SerializeField, Min(0.5f)]
        private float scatterRadius = 1.8f;

        [SerializeField]
        private Transform model;

        /// <summary>Raised once when the chest is opened, before it vanishes.</summary>
        public event Action Opened;

        private bool opened;
        private int dungeonRing = 1;

        /// <summary>Configures this chest for the same room-depth progression as its enemies.</summary>
        public void ConfigureForRoom(int ring)
        {
            dungeonRing = Mathf.Max(1, ring);
        }

        /// <summary>Calculates a chest orb's reward at a dungeon ring.</summary>
        public static int ScaledExperiencePerPickup(
            int baseExperience,
            float growthPerRing,
            int ring
        )
        {
            float multiplier = 1f + Mathf.Max(0, ring - 1) * Mathf.Max(0f, growthPerRing);
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, baseExperience) * multiplier));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (opened)
                return;
            PlayerStats playerStats = other.GetComponentInParent<PlayerStats>();
            if (playerStats == null)
                return;

            opened = true;
            AutoplayFeatureLog.Record(AutoplayFeatures.ChestOpened);
            Opened?.Invoke();

            foreach (Collider chestCollider in GetComponentsInChildren<Collider>())
                chestCollider.enabled = false;

            SpawnLoot(playerStats);
            StartCoroutine(PopAndVanish());
        }

        private void SpawnLoot(PlayerStats playerStats)
        {
            Vector2 center = transform.position.ToGround();
            SpawnExperience(center, playerStats);

            GameObject[] boostPrefabs = FindAnyObjectByType<BoostHandler>()?.BoostPrefabs;
            if (boostPrefabs == null || boostPrefabs.Length == 0)
                return;

            for (int i = 0; i < boostDropCount; i++)
            {
                GameObject prefab = PickWeightedBoost(boostPrefabs);
                if (prefab == null)
                    return;
                Vector2 spot = center + ScatterOffset(i, boostDropCount) * 1.4f;
                Instantiate(prefab, spot.ToWorld(), Quaternion.identity);
            }
        }

        private void SpawnExperience(Vector2 center, PlayerStats playerStats) =>
            SpawnExperience(
                center,
                playerStats,
                position => PoolManager.Instance?.GetExpGain(position)
            );

        internal void SpawnExperience(
            Vector2 center,
            PlayerStats playerStats,
            Func<Vector3, ExpGain> getExperience
        )
        {
            if (expDropCount <= 0)
                return;

            int experiencePerOrb = ScaledExperiencePerPickup(
                experiencePerPickup,
                experienceGrowthPerRing,
                dungeonRing
            );
            int totalExperience = experiencePerOrb * expDropCount;
            var spawned = new List<ExpGain>(expDropCount);
            var landingPositions = new List<Vector3>(expDropCount);
            Vector3 launchPosition = center.ToWorld(0.5f);

            for (int i = 0; i < expDropCount; i++)
            {
                Vector2 spot = center + ScatterOffset(i, expDropCount);
                ExpGain gain = getExperience(launchPosition);
                if (gain == null)
                    break;
                spawned.Add(gain);
                landingPositions.Add(spot.ToWorld(0.5f));
            }

            // A busy room can exhaust the shared orb pool. Preserve the chest's
            // complete reward by concentrating it into the orbs that were available.
            if (spawned.Count == 0)
            {
                playerStats.ApplyExperience(totalExperience);
                return;
            }

            InitializeExperienceDrops(spawned, landingPositions, totalExperience);
        }

        internal static void InitializeExperienceDrops(
            List<ExpGain> spawned,
            List<Vector3> landingPositions,
            int totalExperience
        )
        {
            int experienceEach = totalExperience / spawned.Count;
            int remainder = totalExperience % spawned.Count;
            for (int i = 0; i < spawned.Count; i++)
            {
                spawned[i]
                    .InitDropped(
                        experienceEach + (i < remainder ? 1 : 0),
                        landingPositions[i],
                        ExpGain.DropStyle.Chest
                    );
            }
        }

        private Vector2 ScatterOffset(int index, int total)
        {
            float angle =
                (index / (float)Mathf.Max(1, total)) * 2f * Mathf.PI
                + UnityEngine.Random.Range(-0.4f, 0.4f);
            float distance = UnityEngine.Random.Range(scatterRadius * 0.5f, scatterRadius);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }

        /// <summary>Rolls one boost prefab weighted by <see cref="BoostBase.DropWeight"/>.
        /// Shared with elite kill rewards (see DungeonEnemyPlacer).</summary>
        public static GameObject PickWeightedBoost(GameObject[] prefabs) =>
            PickWeightedBoost(prefabs, totalWeight => UnityEngine.Random.value * totalWeight);

        internal static GameObject PickWeightedBoost(
            GameObject[] prefabs,
            System.Func<float, float> rollForTotal
        )
        {
            if (prefabs == null || prefabs.Length == 0)
                return null;

            float totalWeight = 0f;
            foreach (GameObject prefab in prefabs)
            {
                BoostBase boost = prefab != null ? prefab.GetComponent<BoostBase>() : null;
                if (boost != null)
                    totalWeight += Mathf.Max(0f, boost.DropWeight);
            }
            if (totalWeight <= 0f)
                return null;

            float roll = rollForTotal(totalWeight);
            foreach (GameObject prefab in prefabs)
            {
                BoostBase boost = prefab != null ? prefab.GetComponent<BoostBase>() : null;
                if (boost == null)
                    continue;
                roll -= Mathf.Max(0f, boost.DropWeight);
                if (roll <= 0f)
                    return prefab;
            }
            return prefabs[prefabs.Length - 1];
        }

        private IEnumerator PopAndVanish()
        {
            Transform visual = model != null ? model : transform;
            Vector3 baseScale = visual.localScale;

            const float popDuration = 0.28f;
            for (float t = 0f; t < popDuration; t += Time.deltaTime)
            {
                float pop = 1f + 0.35f * Mathf.Sin(Mathf.PI * (t / popDuration));
                visual.localScale = baseScale * pop;
                yield return null;
            }

            const float shrinkDuration = 0.22f;
            for (float t = 0f; t < shrinkDuration; t += Time.deltaTime)
            {
                visual.localScale = baseScale * Mathf.Max(0f, 1f - t / shrinkDuration);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
