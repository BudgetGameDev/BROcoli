using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gives dungeon enemies wall-aware pathfinding without touching their AI.
/// Enemies steer straight at <see cref="EnemyBase.player"/>; when a wall
/// blocks the direct line this component swaps that target for a proxy
/// transform that it walks along a NavMesh path (baked by
/// <see cref="DungeonManager"/>), so enemies round corners and use doorways
/// instead of hugging walls. With a clear line the real player is restored,
/// which also keeps melee attacks and damage untouched.
/// </summary>
[DisallowMultipleComponent]
public class DungeonEnemyNavigator : MonoBehaviour
{
    private const float RepathInterval = 0.35f;
    private const float CornerReachedSq = 1.2f;
    private const float SampleMaxDistance = 3f;

    private EnemyBase enemy;
    private Transform realPlayer;
    private Transform proxy;
    private NavMeshPath path;
    private int wallMask;
    private float nextRepath;

    private void Awake()
    {
        enemy = GetComponent<EnemyBase>();
        path = new NavMeshPath();
        wallMask = LayerMask.GetMask("Wall");
        // Stagger repaths so a room full of enemies doesn't path on one frame.
        nextRepath = Time.time + Random.value * RepathInterval;
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
        if (enemy == null || !enemy.enabled)
            return;

        // EnemyBase.OnEnable re-resolves the real player; capture it whenever
        // the field holds anything other than our proxy.
        if (enemy.player != null && enemy.player != proxy)
            realPlayer = enemy.player;
        if (realPlayer == null)
            return;

        if (Time.time < nextRepath)
            return;
        nextRepath = Time.time + RepathInterval;
        SteerTowardPlayer();
    }

    private void SteerTowardPlayer()
    {
        Vector2 from = transform.position.ToGround();
        Vector2 to = realPlayer.position.ToGround();
        Vector2 delta = to - from;
        float distance = delta.magnitude;

        bool blocked =
            distance > 0.5f
            && Physics.Raycast(
                from.ToWorld(1f),
                delta.ToWorld(0f).normalized,
                distance,
                wallMask,
                QueryTriggerInteraction.Ignore
            );

        if (!blocked)
        {
            enemy.player = realPlayer;
            return;
        }

        if (
            !NavMesh.SamplePosition(
                from.ToWorld(),
                out NavMeshHit fromHit,
                SampleMaxDistance,
                NavMesh.AllAreas
            )
            || !NavMesh.SamplePosition(
                to.ToWorld(),
                out NavMeshHit toHit,
                SampleMaxDistance,
                NavMesh.AllAreas
            )
            || !NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, path)
            || path.corners.Length < 2
        )
        {
            enemy.player = realPlayer;
            return;
        }

        Vector3 corner = path.corners[1];
        if (path.corners.Length > 2 && (corner.ToGround() - from).sqrMagnitude < CornerReachedSq)
        {
            corner = path.corners[2];
        }

        if (proxy == null)
            proxy = new GameObject(name + " NavProxy").transform;
        proxy.position = corner.ToGround().ToWorld(realPlayer.position.y);
        enemy.player = proxy;
    }
}
