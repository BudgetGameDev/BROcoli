using UnityEngine;

/// <summary>
/// Autoplay bot (Phase 3). Plays a kiting game: the player's spray auto-aims at the
/// enemy cluster regardless of facing, so the bot kills at the far edge of spray
/// range while retreating from pursuers. It engages to farm XP/levels, but backs off
/// when an enemy gets too close, when it gets crowded, or when HP is low — and avoids
/// the arena edge. This lets it both level up (progress) and survive (survive).
///
/// Combat is automatic, so the bot only produces a movement vector. Inert unless
/// <see cref="Active"/> is set (see PlayerInputHandler.UpdateInput).
/// </summary>
public class BotDriver : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static Vector2 Move { get; private set; }

    // Spray reaches ~4.7 (SpraySettings.BaseSprayRange); kill near that edge.
    [SerializeField] private float senseRadius = 12f;
    [SerializeField] private float engageRadius = 4.2f;     // hold near the spray's edge
    [SerializeField] private float dangerRadius = 3f;       // start backing off (kite) here
    [SerializeField] private float strafeWeight = 0.3f;
    [SerializeField] private int crowdCount = 4;            // this many close enemies => retreat
    [SerializeField] private float lowHpFraction = 0.35f;   // retreat below this HP
    [SerializeField] private float centerPull = 0.4f;
    [SerializeField] private float maxCenterDistance = 7f;

    private Transform _player;
    private PlayerStats _stats;

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
        Vector2 move;

        Vector2 centroid = Vector2.zero;
        Vector2 repulsion = Vector2.zero;
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

                centroid += ep;
                count++;
                if (d < nearest) nearest = d;
                if (d < engageRadius + 1f) closeCount++;
                if (d < dangerRadius * 1.5f)
                    repulsion += (away / d) * ((dangerRadius * 1.5f - d) / (dangerRadius * 1.5f));
            }
        }

        if (count == 0)
        {
            move = -pos * 0.15f; // no threats: drift toward center
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
                // Kite: move away from the cluster (+ arc) while the spray keeps hitting pursuers.
                Vector2 flee = repulsion.sqrMagnitude > 0.0001f ? repulsion.normalized : radial;
                move = flee + strafe;
            }
            else
            {
                // Hold near the spray edge: approach if too far, ease off if too close.
                float distError = clusterDist - engageRadius;
                Vector2 approach = radial * -Mathf.Clamp(distError, -1f, 1f);
                move = approach + strafe + repulsion * 0.5f;
            }
        }

        float fromCenter = pos.magnitude;
        if (fromCenter > maxCenterDistance)
            move += -pos.normalized * (centerPull * (fromCenter - maxCenterDistance));

        Move = move.sqrMagnitude > 1f ? move.normalized : move;
    }
}
