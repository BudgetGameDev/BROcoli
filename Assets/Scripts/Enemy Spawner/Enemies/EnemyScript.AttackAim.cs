using UnityEngine;

public partial class EnemyScript
{
    /// <summary>
    /// Tracks the player while the windup is still telegraphing. The direction
    /// is committed when the strike begins, so a released attack remains
    /// dodgeable instead of homing through its contact frame.
    /// </summary>
    private void RefreshAttackAim()
    {
        if (player == null)
            return;

        Vector2 toPlayer = player.position.ToGround() - transform.position.ToGround();
        if (toPlayer.sqrMagnitude > 0.0001f)
            attackDirection = toPlayer.normalized;

        if (visualTransform == null)
            return;

        // The reach values are world-space. Imported FBX scales otherwise
        // magnify them when applied directly as a local position.
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
