using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gives dungeon enemies prop- and wall-aware steering without replacing their
/// existing Rigidbody movement or combat AI. A proxy target follows the baked
/// NavMesh path whenever the direct route is obstructed, then hands targeting
/// back to the real player as soon as the final approach is clear.
/// </summary>
[DisallowMultipleComponent]
public partial class DungeonEnemyNavigator : MonoBehaviour
{
    private const float RepathInterval = 0.2f;
    private const float CornerReachedDistance = 1.35f;
    private const float SampleMaxDistance = 3f;
    private const float ObstacleProbeHeight = 0.75f;
    private const float ObstacleProbeRadius = 0.48f;
    private const float ObstacleProbeDistance = 1.5f;
    private const float RecoveryDistance = 1.8f;

    private readonly RaycastHit[] obstacleHits = new RaycastHit[16];

    private EnemyBase enemy;
    private Transform realPlayer;
    private Transform proxy;
    private NavMeshPath path;
    private float nextRepath;

    private void Awake()
    {
        enemy = GetComponent<EnemyBase>();
        path = new NavMeshPath();
        InitializeRecovery();

        // Stagger expensive path queries across a room's enemies.
        nextRepath = Time.time + Random.value * RepathInterval;
    }

    private void OnEnable()
    {
        ResetRecovery();
    }

    private void OnDisable()
    {
        // Never leave a pooled enemy chasing a stale proxy.
        if (enemy != null && realPlayer != null && enemy.player == proxy)
            enemy.player = realPlayer;
    }

    private void OnDestroy()
    {
        if (proxy != null)
            Destroy(proxy.gameObject);
    }

    private void FixedUpdate()
    {
        if (enemy == null || !enemy.enabled || enemy.IsDying)
            return;

        // EnemyBase.OnEnable resolves the real player. Capture it whenever the
        // target is anything other than this navigator's proxy.
        if (enemy.player != null && enemy.player != proxy)
            realPlayer = enemy.player;
        if (realPlayer == null)
            return;

        if (Time.time >= nextProgressCheck)
            CheckProgress();

        if (Time.time < recoveryUntil)
        {
            SetProxyTarget(recoveryTarget);
            return;
        }

        if (Time.time < nextRepath)
            return;

        nextRepath = Time.time + RepathInterval;
        SteerTowardPlayer();
    }

    private void SteerTowardPlayer()
    {
        Vector3 from = transform.position;
        Vector3 to = realPlayer.position;
        if (
            !NavMesh.SamplePosition(
                from,
                out NavMeshHit fromHit,
                SampleMaxDistance,
                NavMesh.AllAreas
            )
            || !NavMesh.SamplePosition(
                to,
                out NavMeshHit toHit,
                SampleMaxDistance,
                NavMesh.AllAreas
            )
            || !NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, path)
            || path.corners.Length < 2
        )
        {
            SteerDirectlyOrSlide(from, to);
            return;
        }

        Vector3[] corners = path.corners;
        bool completeDirectPath =
            path.status == NavMeshPathStatus.PathComplete && corners.Length == 2;
        Vector3 slideTarget = default;
        if (completeDirectPath && !TryGetObstacleSlide(from, to, fromHit.position, out slideTarget))
        {
            enemy.player = realPlayer;
            return;
        }

        Vector3 steeringTarget;
        if (completeDirectPath)
        {
            steeringTarget = slideTarget;
        }
        else
        {
            int cornerIndex = 1;
            float reachedSq = CornerReachedDistance * CornerReachedDistance;
            while (
                cornerIndex < corners.Length - 1
                && (corners[cornerIndex].ToGround() - from.ToGround()).sqrMagnitude < reachedSq
            )
            {
                cornerIndex++;
            }

            steeringTarget = corners[cornerIndex];
            if (
                TryGetObstacleSlide(
                    from,
                    steeringTarget,
                    fromHit.position,
                    out Vector3 adjustedTarget
                )
            )
                steeringTarget = adjustedTarget;
        }

        SetProxyTarget(steeringTarget);
    }

    private void SteerDirectlyOrSlide(Vector3 from, Vector3 to)
    {
        Vector3 sampleOrigin = from.ToGround().ToWorld();
        if (TryGetObstacleSlide(from, to, sampleOrigin, out Vector3 slideTarget))
            SetProxyTarget(slideTarget);
        else
            enemy.player = realPlayer;
    }

    private bool TryGetObstacleSlide(
        Vector3 from,
        Vector3 desiredTarget,
        Vector3 navMeshOrigin,
        out Vector3 adjustedTarget
    )
    {
        adjustedTarget = default;
        Vector2 desired = desiredTarget.ToGround() - from.ToGround();
        float distance = desired.magnitude;
        if (distance < 0.05f)
            return false;

        Vector2 direction = desired / distance;
        Vector3 origin = from.ToGround().ToWorld(ObstacleProbeHeight);
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            ObstacleProbeRadius,
            direction.ToWorld(),
            obstacleHits,
            Mathf.Min(distance, ObstacleProbeDistance),
            ~0,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit nearest = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = obstacleHits[i];
            if (!IsNavigationObstacle(candidate.collider) || candidate.distance >= nearestDistance)
                continue;
            nearest = candidate;
            nearestDistance = candidate.distance;
        }
        if (nearest.collider == null)
            return false;

        Vector2 normal = nearest.normal.ToGround();
        Vector2 tangent =
            normal.sqrMagnitude > 0.001f
                ? new Vector2(-normal.y, normal.x).normalized
                : new Vector2(-direction.y, direction.x);
        float tangentDot = Vector2.Dot(tangent, direction);
        if (Mathf.Abs(tangentDot) < 0.05f ? recoverySide < 0 : tangentDot < 0f)
            tangent = -tangent;

        Vector2 slideDirection = (tangent * 0.9f + direction * 0.25f).normalized;
        Vector3 requested = (from.ToGround() + slideDirection * RecoveryDistance).ToWorld();
        if (
            !NavMesh.SamplePosition(requested, out NavMeshHit slideHit, 0.8f, NavMesh.AllAreas)
            || NavMesh.Raycast(navMeshOrigin, slideHit.position, out _, NavMesh.AllAreas)
        )
            return false;

        adjustedTarget = slideHit.position;
        return true;
    }

    private bool IsNavigationObstacle(Collider candidate)
    {
        if (candidate == null || candidate.transform.IsChildOf(transform))
            return false;
        if (realPlayer != null && candidate.transform.IsChildOf(realPlayer))
            return false;

        // Crowd steering is handled by EnemyBase's separation. Treating other
        // enemies as static obstacles makes groups deadlock in doorways.
        return candidate.GetComponentInParent<EnemyBase>() == null;
    }

    private void SetProxyTarget(Vector3 target)
    {
        if (proxy == null)
            proxy = new GameObject(name + " NavProxy").transform;
        proxy.position = target.ToGround().ToWorld(realPlayer.position.y);
        enemy.player = proxy;
    }
}
