using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseExpGain(PlayerStats stats)
        {
            PoolManager pool = PoolManager.Instance;
            ExpGain pooled = pool.GetExpGain(stats.transform.position + Vector3.right);
            if (pooled != null)
            {
                pooled.SetPooled(true);
                pooled.Init(1);
                InvokeHierarchy(pooled, "TryCollect", stats.GetComponent<Collider>());
            }

            GameObject collectedPooledObject = NewExperience(
                "Coverage collected pooled experience"
            );
            ExpGain collectedPooled = collectedPooledObject.GetComponent<ExpGain>();
            collectedPooled.SetPooled(true);
            collectedPooled.Init(1);
            InvokeHierarchy(collectedPooled, "TryCollect", stats.GetComponent<Collider>());

            GameObject ordinaryObject = NewExperience("Coverage ordinary experience");
            ExpGain ordinary = ordinaryObject.GetComponent<ExpGain>();
            ordinary.Init(1);
            InvokeHierarchy(ordinary, "TryCollect", stats.GetComponent<Collider>());

            GameObject dropObject = NewExperience("Coverage dropping experience");
            ExpGain drop = dropObject.GetComponent<ExpGain>();
            drop.SetPooled(true);
            drop.InitDropped(
                1,
                stats.transform.position + Vector3.forward,
                ExpGain.DropStyle.Chest
            );
            InvokeHierarchy(drop, "TryCollect", stats.GetComponent<Collider>());
            SetHierarchyField(drop, "rb", null);
            SetHierarchyField(drop, "_dropElapsed", 100f);
            InvokeHierarchy(drop, "AdvanceDrop");
            SetHierarchyField(drop, "_landingSettleRemaining", 0f);
            InvokeHierarchy(drop, "FixedUpdate");
            InvokeHierarchy(drop, "RestoreGroundedPhysics");
            Object.Destroy(dropObject);
        }

        private static GameObject NewExperience(string name)
        {
            GameObject item = new(name);
            item.AddComponent<SphereCollider>();
            item.AddComponent<Rigidbody>();
            ExpGain experience = item.AddComponent<ExpGain>();
            InvokeHierarchy(experience, "Awake");
            InvokeHierarchy(experience, "OnEnable");
            return item;
        }
    }
}
