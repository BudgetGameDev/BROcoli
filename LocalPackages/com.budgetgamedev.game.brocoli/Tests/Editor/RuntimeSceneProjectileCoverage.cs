using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseEnemyProjectileWallSweep(Vector3 playerPosition)
        {
            GameObject projectileObject = new("Coverage swept enemy projectile");
            projectileObject.transform.position = playerPosition;
            projectileObject.AddComponent<SphereCollider>().isTrigger = true;
            projectileObject.AddComponent<Rigidbody>().useGravity = false;
            EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
            InvokeHierarchy(projectile, "Awake");
            SetHierarchyField(projectile, "travelDirection", Vector2.right);

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Coverage projectile wall";
            wall.layer = LayerMask.NameToLayer("Wall");
            wall.transform.position = playerPosition + Vector3.right * 0.1f;
            Physics.SyncTransforms();
            InvokeHierarchy(projectile, "FixedUpdate");

            GameObject legacyObject = new("Coverage swept player projectile");
            legacyObject.transform.position = playerPosition;
            SphereCollider legacyCollider = legacyObject.AddComponent<SphereCollider>();
            Rigidbody legacyBody = legacyObject.AddComponent<Rigidbody>();
            legacyBody.useGravity = false;
            Projectile legacy = legacyObject.AddComponent<Projectile>();
            InvokeHierarchy(legacy, "Awake");
            legacy.Init(Vector2.right, 1f);
            InvokeHierarchy(legacy, "Update");
            InvokeHierarchy(legacy, "OnTriggerEnter", wall.GetComponent<Collider>());
            Assert.That(legacyCollider.enabled, Is.False);
            Object.Destroy(wall);
            Object.Destroy(projectileObject);
            Object.Destroy(legacyObject);
        }
    }
}
