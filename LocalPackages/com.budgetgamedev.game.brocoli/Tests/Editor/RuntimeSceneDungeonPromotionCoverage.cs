using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseChestExperienceDistribution()
        {
            var gains = new List<ExpGain>();
            var positions = new List<Vector3>();
            for (int index = 0; index < 2; index++)
            {
                GameObject orb = new($"Coverage Chest Orb {index}");
                orb.AddComponent<Rigidbody>();
                orb.AddComponent<SphereCollider>();
                gains.Add(orb.AddComponent<ExpGain>());
                positions.Add(new Vector3(index, 0.5f, index));
            }
            LootChest.InitializeExperienceDrops(gains, positions, 5);
            GameObject chestObject = new("Coverage Experience Distributor");
            LootChest chest = chestObject.AddComponent<LootChest>();
            SetHierarchyField(chest, "expDropCount", 2);
            int next = 0;
            chest.SpawnExperience(Vector2.zero, PlayerStats.Resolve(), _ => gains[next++]);
            chest.SpawnExperience(Vector2.zero, PlayerStats.Resolve(), _ => null);
            foreach (ExpGain gain in gains)
                Object.Destroy(gain.gameObject);
            Object.Destroy(chestObject);
        }
    }
}
