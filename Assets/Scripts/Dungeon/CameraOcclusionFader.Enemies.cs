using UnityEngine;

public sealed partial class CameraOcclusionFader
{
    private const float EnemyTargetRefreshInterval = 0.25f;

    [Tooltip(
        "How close the player must come to an enemy in another room before that enemy may lower a wall. "
            + "Lowering a wall is visible from anywhere on screen, so an unreached enemy would announce "
            + "the contents of a room the player has not entered yet."
    )]
    [SerializeField, Min(0f)]
    private float enemyApproachRadius = EnemyRevealGate.DefaultApproachRadius;

    private EnemyBase[] enemyTargets = System.Array.Empty<EnemyBase>();
    private float nextEnemyTargetRefreshTime;

    public int VisibleEnemyTargetCount { get; private set; }

    private void AddEnemyTargets()
    {
        VisibleEnemyTargetCount = 0;
        if (target == null)
            return;

        if (Time.unscaledTime >= nextEnemyTargetRefreshTime)
        {
            enemyTargets = Object.FindObjectsByType<EnemyBase>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
            nextEnemyTargetRefreshTime = Time.unscaledTime + EnemyTargetRefreshInterval;
        }

        foreach (EnemyBase enemy in enemyTargets)
        {
            if (
                enemy == null
                || !enemy.gameObject.activeInHierarchy
                || enemy.IsDying
                || !EnemyRevealGate.IsRevealed(
                    target.position,
                    enemy.transform.position,
                    enemyApproachRadius
                )
                || !TryGetTargetBounds(enemy.transform, out Bounds bounds)
                || !OcclusionTarget.TryCreate(
                    cameraModel,
                    OcclusionTargetKind.Enemy,
                    enemy.transform.position,
                    bounds,
                    minimumEnemyCoverage,
                    out OcclusionTarget enemyTarget
                )
            )
                continue;

            VisibleEnemyTargetCount++;
            Resolver.AddTarget(enemyTarget);
        }
    }
}
