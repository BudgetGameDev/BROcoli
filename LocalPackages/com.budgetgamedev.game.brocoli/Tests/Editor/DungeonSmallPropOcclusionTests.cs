using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonSmallPropOcclusionTests
    {
        private const string PrefabFolder =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/";

        [TestCase("DungeonChest.prefab")]
        [TestCase("DungeonChestGolden.prefab")]
        public void ChestsStayVisibleInsteadOfJoiningTheFadeSystem(string prefabName)
        {
            GameObject room = DungeonPropFixtures.RoomRoot();
            GameObject instance = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabFolder + prefabName
                );
                Assert.That(prefab, Is.Not.Null, prefabName);

                instance = Object.Instantiate(prefab, room.transform);
                Collider solidCollider = SolidCollider(instance);
                Assert.That(solidCollider, Is.Not.Null, $"{prefabName} has no solid body");

                Assert.That(
                    DungeonOccluder.Owning(solidCollider),
                    Is.Null,
                    $"{prefabName} was adopted by the half-height fade system"
                );
                Assert.That(instance.GetComponent<DungeonOccluder>(), Is.Null);
            }
            finally
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void UnknownLowProfilePropAlsoStaysVisible()
        {
            GameObject room = DungeonPropFixtures.RoomRoot();
            try
            {
                GameObject prop = DungeonPropFixtures.NovelProp(
                    room.transform,
                    new Vector3(2f, 1.4f, 1f),
                    Vector3.zero
                );

                Assert.That(
                    DungeonOccluder.Owning(prop.GetComponentInChildren<Collider>()),
                    Is.Null
                );
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        private static Collider SolidCollider(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (!collider.isTrigger)
                    return collider;
            }
            return null;
        }
    }
}
