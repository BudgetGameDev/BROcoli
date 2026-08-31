using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static bool meleeAudioEdgesCovered;

        private static void ExerciseEnemyAudio(EnemyBase enemy)
        {
            ProceduralEnemyMeleeAudio meleeAudio = enemy.GetComponent<ProceduralEnemyMeleeAudio>();
            if (meleeAudio == null || meleeAudioEdgesCovered)
                return;

            meleeAudioEdgesCovered = true;
            SetHierarchyField(meleeAudio, "cachedClips", null);
            SetHierarchyField(meleeAudio, "playerTransform", null);
            meleeAudio.PlayMeleeSound(0f);
            SetHierarchyField(meleeAudio, "playerTransform", enemy.transform);
            meleeAudio.PlayMeleeSound(0f);
        }

        private static void ExerciseMeleeStateMachine(EnemyBase enemy)
        {
            Transform target = enemy.player;
            enemy.player = null;
            InvokeHierarchy(enemy, "StartAttackAnimation");
            enemy.player = target;
            SetHierarchyField(enemy, "attackWindupDuration", 0f);
            SetHierarchyField(enemy, "attackStrikeDuration", 0f);
            SetHierarchyField(enemy, "attackRecoverDuration", 0f);
            SetHierarchyField(enemy, "meleeRange", 10f);
            SetHierarchyField(enemy, "attackLungeDistance", 0.42f);
            InvokeHierarchy(enemy, "StartAttackAnimation");
            InvokeHierarchy(enemy, "UpdateAttackAnimation");
            InvokeHierarchy(enemy, "UpdateAttackAnimation");
            InvokeHierarchy(enemy, "UpdateAttackAnimation");
            InvokeHierarchy(enemy, "UpdateAttackAnimation");
            InvokeHierarchy(enemy, "EaseOutQuad", 0.5f);
            InvokeHierarchy(enemy, "EaseInCubic", 0.5f);
            InvokeHierarchy(enemy, "GetAttackReach");
            InvokeHierarchy(enemy, "StartAttackAnimation");
            enemy.TakeDamage(1f, Vector2.right);
            enemy.ResetForPool();
        }
    }
}
