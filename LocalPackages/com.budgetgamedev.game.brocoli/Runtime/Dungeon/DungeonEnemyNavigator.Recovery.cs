using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonEnemyNavigator
    {
        private const float ProgressCheckInterval = 0.45f;
        private const float MinimumProgress = 0.12f;
        private const float StuckRecoveryDelay = 0.9f;
        private const float RecoveryDuration = 0.7f;

        private static readonly float[] RecoveryAngles = { 55f, -55f, 90f, -90f, 135f, -135f };

        private float nextProgressCheck;
        private float stationaryTime;
        private float recoveryUntil;
        private Vector2 lastProgressPosition;
        private Vector3 recoveryTarget;
        private int recoverySide;

        private void InitializeRecovery()
        {
            recoverySide = (GetInstanceID() & 1) == 0 ? 1 : -1;
            ResetRecovery();
        }

        private void ResetRecovery()
        {
            stationaryTime = 0f;
            recoveryUntil = 0f;
            lastProgressPosition = transform.position.ToGround();
            nextProgressCheck = Time.time + ProgressCheckInterval;
        }

        private void CheckProgress()
        {
            Vector2 position = transform.position.ToGround();
            float elapsed = Mathf.Max(
                ProgressCheckInterval,
                Time.time - nextProgressCheck + ProgressCheckInterval
            );
            nextProgressCheck = Time.time + ProgressCheckInterval;

            bool navigating = proxy != null && enemy.player == proxy;
            float progressSq = (position - lastProgressPosition).sqrMagnitude;
            if (navigating && progressSq < MinimumProgress * MinimumProgress)
                stationaryTime += elapsed;
            else
                stationaryTime = 0f;
            lastProgressPosition = position;

            if (stationaryTime < StuckRecoveryDelay || !TryPickRecoveryTarget(out recoveryTarget))
                return;

            stationaryTime = 0f;
            recoverySide = -recoverySide;
            recoveryUntil = Time.time + RecoveryDuration;
            nextRepath = recoveryUntil;
            SetProxyTarget(recoveryTarget);
        }

        private bool TryPickRecoveryTarget(out Vector3 target)
        {
            target = default;
            if (
                !NavMesh.SamplePosition(
                    transform.position,
                    out NavMeshHit originHit,
                    SampleMaxDistance,
                    NavMesh.AllAreas
                )
            )
                return false;

            Vector2 desired =
                proxy != null
                    ? proxy.position.ToGround() - transform.position.ToGround()
                    : realPlayer.position.ToGround() - transform.position.ToGround();
            if (desired.sqrMagnitude < 0.01f)
                return false;

            for (int i = 0; i < RecoveryAngles.Length; i++)
            {
                float angle = RecoveryAngles[i] * recoverySide;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * desired.normalized;
                Vector3 requested = (
                    transform.position.ToGround() + direction * RecoveryDistance
                ).ToWorld();
                if (
                    !NavMesh.SamplePosition(requested, out NavMeshHit hit, 0.65f, NavMesh.AllAreas)
                    || NavMesh.Raycast(originHit.position, hit.position, out _, NavMesh.AllAreas)
                    || TryGetObstacleSlide(
                        transform.position,
                        hit.position,
                        originHit.position,
                        out _
                    )
                )
                    continue;

                target = hit.position;
                return true;
            }

            return false;
        }
    }
}
