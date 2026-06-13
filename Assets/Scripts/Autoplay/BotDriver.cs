using UnityEngine;

/// <summary>
/// Autoplay bot. Produces a movement vector for the player by steering away from
/// nearby enemies, with a gentle pull back toward the arena center so it does not
/// drift off-screen. Combat is automatic (PlayerCombat auto-targets and fires), so
/// a movement vector is all the bot needs to control.
///
/// While this component is enabled, <see cref="Active"/> is true and
/// PlayerInputHandler reads <see cref="Move"/> instead of keyboard/joystick input.
/// </summary>
public class BotDriver : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static Vector2 Move { get; private set; }

    [SerializeField] private float threatRadius = 9f;
    [SerializeField] private float centerPull = 0.35f;
    [SerializeField] private float maxCenterDistance = 6f;

    private Transform _player;

    private void OnEnable() { Active = true; }

    private void OnDisable()
    {
        Active = false;
        Move = Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (_player == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null)
            {
                Move = Vector2.zero;
                return;
            }
            _player = playerGo.transform;
        }

        Vector2 pos = _player.position;
        Vector2 steer = Vector2.zero;

        var hash = EnemySpatialHash.Instance;
        if (hash != null)
        {
            var enemies = hash.GetNearbyEnemies(pos, threatRadius);
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                Vector2 away = pos - (Vector2)enemy.transform.position;
                float dist = away.magnitude;
                if (dist < 0.0001f) continue;
                // Closer enemies push harder (linear falloff to the threat radius).
                steer += (away / dist) * ((threatRadius - dist) / threatRadius);
            }
        }

        // Gentle pull back toward center when wandering too far out.
        float distFromCenter = pos.magnitude;
        if (distFromCenter > maxCenterDistance)
        {
            steer += -pos.normalized * (centerPull * (distFromCenter - maxCenterDistance));
        }

        Move = steer.sqrMagnitude > 1f ? steer.normalized : steer;
    }
}
