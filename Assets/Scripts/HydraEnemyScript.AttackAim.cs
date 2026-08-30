using UnityEngine;

public partial class HydraEnemyScript
{
    private void RefreshAttackAim()
    {
        if (player == null)
            return;

        Vector2 toPlayer = player.position.ToGround() - transform.position.ToGround();
        if (toPlayer.sqrMagnitude > 0.0001f)
            attackDirection = toPlayer.normalized;

        if (visualTransform == null)
            return;

        Vector3 worldLunge = (attackDirection * activeAttackReach).ToWorld();
        Vector3 worldPullBack = (
            -attackDirection * Mathf.Clamp(attackPullBackDistance, 0f, MaxAttackPullBackDistance)
        ).ToWorld();
        Vector3 localLunge =
            visualTransform.parent != null
                ? visualTransform.parent.InverseTransformVector(worldLunge)
                : worldLunge;
        Vector3 localPullBack =
            visualTransform.parent != null
                ? visualTransform.parent.InverseTransformVector(worldPullBack)
                : worldPullBack;
        attackWindupPos = attackStartPos + localPullBack;
        attackTargetPos = attackStartPos + localLunge;
    }
}
