using UnityEngine;

public sealed partial class CameraOcclusionFader
{
    private void FindEnemyOccludingGeometry()
    {
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
                || !TryGetEnemyViewportRect(enemy, out Rect enemyRect, out Bounds enemyBounds)
            )
                continue;

            VisibleEnemyTargetCount++;
            ScanOcclusionTarget(
                enemyRect,
                enemyBounds,
                enemy.transform.position,
                minimumEnemyCoverage
            );
        }
    }

    private bool TryGetEnemyViewportRect(
        EnemyBase enemy,
        out Rect viewportRect,
        out Bounds enemyBounds
    )
    {
        targetRenderers.Clear();
        enemy.GetComponentsInChildren(false, targetRenderers);
        bool hasBounds = false;
        enemyBounds = default;
        foreach (Renderer enemyRenderer in targetRenderers)
        {
            if (!IsPlayerBodyRenderer(enemyRenderer))
                continue;
            if (!hasBounds)
            {
                enemyBounds = enemyRenderer.bounds;
                hasBounds = true;
            }
            else
                enemyBounds.Encapsulate(enemyRenderer.bounds);
        }
        viewportRect = default;
        return hasBounds && TryProjectBounds(enemyBounds, out viewportRect);
    }
}
