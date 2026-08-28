using System;
using System.Collections;
using Pooling;
using UnityEngine;

/// <summary>
/// A treasure chest placed in dungeon rooms. Walking into it bursts open a
/// scatter of experience pickups plus one or more weighted boost powerups
/// (reusing the wave mode's drop prefabs), then the chest pops and vanishes.
/// </summary>
[DisallowMultipleComponent]
public class LootChest : MonoBehaviour
{
    [SerializeField, Min(0)]
    private int expDropCount = 5;

    [SerializeField, Min(1)]
    private int experiencePerPickup = 3;

    [SerializeField, Min(0)]
    private int boostDropCount = 1;

    [SerializeField, Min(0.5f)]
    private float scatterRadius = 1.8f;

    [SerializeField]
    private Transform model;

    /// <summary>Raised once when the chest is opened, before it vanishes.</summary>
    public event Action Opened;

    private bool opened;

    private void OnTriggerEnter(Collider other)
    {
        if (opened)
            return;
        if (other.GetComponentInParent<PlayerStats>() == null)
            return;

        opened = true;
        Opened?.Invoke();

        foreach (Collider chestCollider in GetComponentsInChildren<Collider>())
            chestCollider.enabled = false;

        SpawnLoot();
        StartCoroutine(PopAndVanish());
    }

    private void SpawnLoot()
    {
        Vector2 center = transform.position.ToGround();

        for (int i = 0; i < expDropCount; i++)
        {
            Vector2 spot = center + ScatterOffset(i, expDropCount);
            ExpGain gain = PoolManager.Instance?.GetExpGain(spot.ToWorld(0.5f));
            if (gain == null)
                break;
            gain.Init(experiencePerPickup);
        }

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
    public static GameObject PickWeightedBoost(GameObject[] prefabs)
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

        float roll = UnityEngine.Random.value * totalWeight;
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
