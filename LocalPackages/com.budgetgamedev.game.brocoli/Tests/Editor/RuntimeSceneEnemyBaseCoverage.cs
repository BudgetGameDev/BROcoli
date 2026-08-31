using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseEnemyColliderEdges(EnemyBase enemy, Transform target)
        {
            Collider bodyCollider = GetHierarchyField<Collider>(enemy, "bodyCollider");
            Collider playerCollider = GetHierarchyField<Collider>(enemy, "playerCollider");
            SetHierarchyField(enemy, "playerCollider", null);
            InvokeHierarchy(enemy, "GetPlayerColliderGap");
            SetHierarchyField(enemy, "bodyCollider", null);
            InvokeHierarchy(enemy, "GetPlayerColliderGap");

            GameObject noColliderTarget = new("Coverage target without collider");
            SetHierarchyField(enemy, "bodyCollider", bodyCollider);
            enemy.player = noColliderTarget.transform;
            SetHierarchyField(enemy, "playerCollider", null);
            InvokeHierarchy(enemy, "GetPlayerColliderGap");
            Object.Destroy(noColliderTarget);

            enemy.player = target;
            SetHierarchyField(enemy, "playerCollider", playerCollider);
        }
    }
}
