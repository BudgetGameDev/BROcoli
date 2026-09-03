using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
        internal readonly struct EnemyObservation
        {
            internal readonly int Count;
            internal readonly int CloseCount;
            internal readonly float NearestDistance;
            internal readonly Vector2 NearestPosition;
            internal readonly Vector2 TargetPosition;
            internal readonly Vector2 Centroid;
            internal readonly Vector2 Repulsion;

            /// <summary>How much of the space around the agent has something in it.</summary>
            internal readonly float Coverage;

            /// <summary>The middle of the widest way out, when there is one.</summary>
            internal readonly Vector2 Escape;

            internal EnemyObservation(
                int count,
                int closeCount,
                float nearestDistance,
                Vector2 nearestPosition,
                Vector2 targetPosition,
                Vector2 centroid,
                Vector2 repulsion,
                float coverage = 0f,
                Vector2 escape = default
            )
            {
                Count = count;
                CloseCount = closeCount;
                NearestDistance = nearestDistance;
                NearestPosition = nearestPosition;
                TargetPosition = targetPosition;
                Centroid = centroid;
                Repulsion = repulsion;
                Coverage = coverage;
                Escape = escape;
            }
        }

        /// <summary>
        /// Kiting distance taken from the weapon the player is actually holding, so a
        /// spray-range upgrade or boost immediately changes how the agent fights
        /// instead of leaving it hugging enemies at a hard-coded radius.
        /// </summary>
        private float EngageRange =>
            stats != null && stats.CurrentSprayRange > 0.5f
                ? stats.CurrentSprayRange * 0.85f
                : engageRadius;

        private EnemyObservation ObserveEnemies(Vector2 position)
        {
            EnemySpatialHash hash = EnemySpatialHash.Instance;
            if (hash == null)
                return new EnemyObservation(
                    0,
                    0,
                    float.PositiveInfinity,
                    position,
                    position,
                    position,
                    default
                );

            float engage = EngageRange;
            Vector2 centroid = Vector2.zero;
            Vector2 repulsion = Vector2.zero;
            Vector2 nearestPosition = position;
            Vector2 targetPosition = position;
            float nearestDistance = float.PositiveInfinity;
            float weakestHealth = float.PositiveInfinity;
            int count = 0;
            int closeCount = 0;
            encirclementBuffer.Clear();

            foreach (EnemyBase enemy in hash.GetNearbyEnemies(position, senseRadius))
            {
                if (enemy == null || enemy.IsDying)
                    continue;

                Vector2 enemyPosition = enemy.transform.position.ToGround();
                Vector2 away = position - enemyPosition;
                float distance = away.magnitude;
                if (distance < 0.001f)
                    continue;

                centroid += enemyPosition;
                count++;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPosition = enemyPosition;
                }
                if (distance < engage + 1f)
                    closeCount++;

                // Focus the weakest reachable enemy: finishing one is worth more than
                // spreading damage, and it thins a crowd faster than picking the nearest.
                float effort = enemy.Health + distance * 4f;
                if (effort < weakestHealth)
                {
                    weakestHealth = effort;
                    targetPosition = enemyPosition;
                }

                float repelRange = dangerRadius * 1.6f;
                if (distance < repelRange)
                    repulsion += away / distance * ((repelRange - distance) / repelRange);
                if (distance <= encirclementRadius)
                    encirclementBuffer.Add(enemyPosition);
            }

            centroid = count > 0 ? centroid / count : position;
            BotEncirclement.Measure(
                position,
                encirclementBuffer,
                encirclementRadius,
                out float coverage,
                out Vector2 escape
            );
            // Remembered for the unsticking manoeuvre, which runs from the progress
            // check rather than from here and has no other view of the crowd.
            lastEscape = coverage >= EncirclementBreakout ? escape : Vector2.zero;
            return new EnemyObservation(
                count,
                closeCount,
                nearestDistance,
                nearestPosition,
                targetPosition,
                centroid,
                repulsion,
                coverage,
                escape
            );
        }

        private Vector2 NavigateCombat(
            Vector2 position,
            EnemyObservation enemies,
            bool forceRetreat
        )
        {
            Vector2 away = position - enemies.Centroid;
            if (away.sqrMagnitude < 0.001f)
                away = position - enemies.NearestPosition;
            if (away.sqrMagnitude < 0.001f)
                away = Vector2.up;
            away.Normalize();

            Vector2 strafe = Vector2.Perpendicular(away) * StrafeSide * strafeWeight;
            if (forceRetreat)
            {
                // Once the crowd is round more than one side, the way out is the gap
                // in it rather than the direction away from its middle -- which, in a
                // ring, is wherever the agent is already standing.
                if (
                    enemies.Coverage >= EncirclementBreakout
                    && enemies.Escape.sqrMagnitude > 0.001f
                )
                    return NavigateLocal(position, enemies.Escape * 2f + enemies.Repulsion * 0.5f);
                return NavigateLocal(position, away + enemies.Repulsion * 1.5f + strafe);
            }

            float engage = EngageRange;
            if (enemies.NearestDistance > engage + 0.65f)
                return NavigateTo(position, enemies.TargetPosition);
            if (enemies.NearestDistance < engage - 0.65f)
                return NavigateLocal(position, away + enemies.Repulsion + strafe);

            Vector2 roomCenter = DungeonLayout.RoomCenter(DungeonLayout.RoomAt(position));
            Vector2 centerPull = Vector2.ClampMagnitude(roomCenter - position, 1f) * 0.2f;
            return NavigateLocal(position, strafe + away * 0.1f + centerPull);
        }

        private Vector2 ComputeProjectileDodge(Vector2 position)
        {
            Vector2 dodge = Vector2.zero;
            int count = GroundPlane.OverlapCircle(
                position,
                projectileSenseRadius,
                projectileBuffer
            );
            for (int i = 0; i < count; i++)
            {
                Collider candidate = projectileBuffer[i];
                if (candidate == null || candidate.GetComponent<EnemyProjectile>() == null)
                    continue;
                TryGetProjectileVelocity(candidate.attachedRigidbody, out Vector2 velocity);
                if (velocity.sqrMagnitude < 0.04f)
                    continue;

                Vector2 toPlayer = position - candidate.transform.position.ToGround();
                Vector2 heading = velocity.normalized;
                float along = Vector2.Dot(toPlayer, heading);
                if (along <= 0f || along >= projectileSenseRadius)
                    continue;

                Vector2 perpendicular = toPlayer - heading * along;
                float missDistance = perpendicular.magnitude;
                if (missDistance >= dodgeRadius)
                    continue;

                Vector2 side =
                    perpendicular.sqrMagnitude > 0.001f
                        ? perpendicular.normalized
                        : Vector2.Perpendicular(heading) * recoverySide;
                float urgency =
                    (1f - missDistance / dodgeRadius) * (1f - along / projectileSenseRadius);
                dodge += side * Mathf.Max(0f, urgency);
            }

            return dodge;
        }

        internal static bool TryGetProjectileVelocity(Rigidbody body, out Vector2 velocity)
        {
            velocity = body != null ? body.GroundVelocity() : Vector2.zero;
            return body != null;
        }
    }
}
