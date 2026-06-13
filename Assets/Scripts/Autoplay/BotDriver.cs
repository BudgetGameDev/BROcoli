using UnityEngine;

/// <summary>
/// Autoplay bot (Phase 3). Plays an active kiting game: stays central where enemies
/// converge, holds the far edge of spray range to farm kills/levels, <b>dodges enemy
/// projectiles</b> (evades perpendicular to an incoming shot's path), and retreats
/// when an enemy gets too close, the area gets crowded, or HP drops — leaning on the
/// meta-build upgrades (lifesteal/regen/speed) to sustain aggression for long runs.
///
/// Combat is automatic (PlayerCombat auto-targets/fires regardless of facing), so the
/// bot only produces a movement vector. Inert unless <see cref="Active"/> is set
/// (see PlayerInputHandler.UpdateInput).
/// </summary>
public class BotDriver : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static Vector2 Move { get; private set; }

    // Spray reaches ~4.7 (SpraySettings.BaseSprayRange); fight near that edge.
    [SerializeField] private float senseRadius = 12f;
    [SerializeField] private float engageRadius = 4.2f;
    [SerializeField] private float dangerRadius = 2.5f;
    [SerializeField] private float strafeWeight = 0.35f;
    [SerializeField] private int crowdCount = 5;          // this many close enemies => retreat
    [SerializeField] private float lowHpFraction = 0.4f;  // retreat below this HP fraction
    [SerializeField] private float centerPull = 0.5f;     // stay central (don't camp the edge)
    [SerializeField] private float maxCenterDistance = 5f;

    [Header("Projectile dodging")]
    [SerializeField] private float projSenseRadius = 8f;
    [SerializeField] private float dodgeRadius = 1.4f;    // evade shots that will pass within this
    [SerializeField] private float dodgeWeight = 3f;      // dodging dominates the move when active

    private Transform _player;
    private PlayerStats _stats;
    private readonly Collider2D[] _projBuf = new Collider2D[64];
    private ContactFilter2D _projFilter;
    private int _frame;
    private Vector2 _lastDodge;

    private void Awake()
    {
        _projFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false, useDepth = false };
    }

    private void OnEnable() => Active = true;

    private void OnDisable()
    {
        Active = false;
        Move = Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (_player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) { Move = Vector2.zero; return; }
            _player = go.transform;
            _stats = go.GetComponent<PlayerStats>();
        }

        Vector2 pos = _player.position;

        // --- enemy cluster sensing ---
        Vector2 centroid = Vector2.zero, repulsion = Vector2.zero;
        float nearest = float.MaxValue;
        int count = 0, closeCount = 0;

        var hash = EnemySpatialHash.Instance;
        if (hash != null)
        {
            foreach (var e in hash.GetNearbyEnemies(pos, senseRadius))
            {
                if (e == null) continue;
                Vector2 ep = e.transform.position;
                Vector2 away = pos - ep;
                float d = away.magnitude;
                if (d < 0.0001f) continue;
                centroid += ep; count++;
                if (d < nearest) nearest = d;
                if (d < engageRadius + 1f) closeCount++;
                if (d < dangerRadius * 1.5f)
                    repulsion += (away / d) * ((dangerRadius * 1.5f - d) / (dangerRadius * 1.5f));
            }
        }

        Vector2 move;
        if (count == 0)
        {
            move = -pos * 0.2f; // no threats sensed: head to center where enemies converge
        }
        else
        {
            centroid /= count;
            Vector2 fromCluster = pos - centroid;
            float clusterDist = fromCluster.magnitude;
            Vector2 radial = clusterDist > 0.0001f ? fromCluster / clusterDist : Vector2.up;
            Vector2 strafe = Vector2.Perpendicular(radial) * strafeWeight;

            float hpFrac = _stats != null && _stats.CurrentMaxHealth > 0f
                ? _stats.CurrentHealth / _stats.CurrentMaxHealth : 1f;
            bool retreat = nearest < dangerRadius || closeCount >= crowdCount || hpFrac < lowHpFraction;

            if (retreat)
            {
                move = (repulsion.sqrMagnitude > 0.0001f ? repulsion.normalized : radial) + strafe;
            }
            else
            {
                float distError = clusterDist - engageRadius;          // >0 far, <0 close
                Vector2 approach = radial * -Mathf.Clamp(distError, -1f, 1f);
                move = approach + strafe + repulsion * 0.5f;
            }
        }

        // --- dodge incoming projectiles (throttled for perf; takes priority) ---
        if (++_frame % 3 == 0)
            _lastDodge = ComputeDodge(pos);
        if (_lastDodge.sqrMagnitude > 0.0001f)
            move += _lastDodge * dodgeWeight;

        // --- stay central (avoid getting cornered / camping the edge) ---
        float fromCenter = pos.magnitude;
        if (fromCenter > maxCenterDistance)
            move += -pos.normalized * (centerPull * (fromCenter - maxCenterDistance));

        Move = move.sqrMagnitude > 1f ? move.normalized : move;
    }

    /// <summary>Sum of perpendicular evasion pushes away from incoming enemy projectiles.</summary>
    private Vector2 ComputeDodge(Vector2 pos)
    {
        Vector2 dodge = Vector2.zero;
        int n = Physics2D.OverlapCircle(pos, projSenseRadius, _projFilter, _projBuf);
        for (int i = 0; i < n; i++)
        {
            var col = _projBuf[i];
            if (col == null || col.GetComponent<EnemyProjectile>() == null) continue;
            var rb = col.attachedRigidbody;
            if (rb == null) continue;
            Vector2 v = rb.linearVelocity;
            if (v.sqrMagnitude < 0.04f) continue;

            Vector2 pp = col.transform.position;
            Vector2 toMe = pos - pp;
            Vector2 vn = v.normalized;
            float along = Vector2.Dot(toMe, vn);
            if (along <= 0f) continue;                 // shot is heading away from us
            Vector2 perp = toMe - vn * along;          // how far its path misses us by
            float miss = perp.magnitude;
            if (miss >= dodgeRadius || along >= projSenseRadius) continue;

            Vector2 side = perp.sqrMagnitude > 0.0001f ? perp.normalized : (Vector2)Vector2.Perpendicular(vn);
            float urgency = (1f - miss / dodgeRadius) * (1f - along / projSenseRadius);
            dodge += side * Mathf.Max(0f, urgency);
        }
        return dodge;
    }
}
