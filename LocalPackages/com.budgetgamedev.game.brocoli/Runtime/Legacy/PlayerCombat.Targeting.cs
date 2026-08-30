using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class PlayerCombat
    {
        private Transform FindTarget(Collider[] hits, Vector2 playerPos, float range)
        {
            // Use smart targeting for spray weapon
            if (_currentWeapon == WeaponType.SanitizerSpray && _sanitizerSpray != null)
            {
                Transform sprayTarget = FindBestSprayTarget(hits, playerPos, range);
                if (sprayTarget != null)
                    return sprayTarget;
            }

            // Fallback: find closest enemy (prefer real enemies over projectiles)
            return FindClosestEnemy(hits, playerPos);
        }

        private Transform FindClosestEnemy(Collider[] hits, Vector2 playerPos)
        {
            Transform closestEnemy = null;
            Transform closestProjectile = null;
            float closestEnemySqrDist = float.MaxValue;
            float closestProjectileSqrDist = float.MaxValue;

            foreach (Collider hit in hits)
            {
                if (!CanShootTarget(hit, playerPos))
                    continue;

                float sqrDist = (hit.transform.position.ToGround() - playerPos).sqrMagnitude;
                EnemyBase enemyComponent = hit.GetComponent<EnemyBase>();

                if (enemyComponent != null)
                {
                    if (sqrDist < closestEnemySqrDist)
                    {
                        closestEnemySqrDist = sqrDist;
                        closestEnemy = hit.transform;
                    }
                }
                else
                {
                    if (sqrDist < closestProjectileSqrDist)
                    {
                        closestProjectileSqrDist = sqrDist;
                        closestProjectile = hit.transform;
                    }
                }
            }

            return closestEnemy ?? closestProjectile;
        }

        private Transform FindBestSprayTarget(Collider[] hits, Vector2 playerPos, float sprayRange)
        {
            if (hits == null || hits.Length == 0)
                return null;

            float sprayAngle = _sanitizerSpray?.SprayWidth ?? 60f;
            float halfAngle = sprayAngle * 0.5f;
            float particleSpeed =
                _sanitizerSpray?.GetParticleSpeed()
                ?? (SpraySettings.BaseSprayRange / SpraySettings.ParticleLifetimeBase);

            // Collect enemies with predicted positions using dynamic velocity-based prediction
            var enemies = new List<(Transform t, EnemyBase e, Vector2 predicted, float dist)>();

            foreach (Collider hit in hits)
            {
                if (!CanShootTarget(hit, playerPos))
                    continue;
                EnemyBase enemy = hit.GetComponent<EnemyBase>();
                if (enemy == null)
                    continue;

                Vector2 enemyPos = hit.bounds.center.ToGround();
                float dist = Vector2.Distance(playerPos, enemyPos);

                if (dist <= sprayRange && dist > 0.1f)
                {
                    Vector2 predicted = GetPredictedEnemyPosition(
                        enemy,
                        enemyPos,
                        dist,
                        particleSpeed
                    );
                    enemies.Add((hit.transform, enemy, predicted, dist));
                }
            }

            if (enemies.Count == 0)
                return null;
            if (enemies.Count == 1)
                return enemies[0].t;

            // Nozzle offset for accurate damage calculations
            float nozzleOffset = SpraySettings.HandOffset + SpraySettings.NozzleLocalPos.x;

            // Find optimal aim direction for maximum total damage
            Transform bestTarget = null;
            float bestTotalDamage = float.MinValue;

            // Sample directions: toward each enemy
            foreach (var primary in enemies)
            {
                // Calculate aim direction from PLAYER to predicted target (consistent with SprayHandVisuals)
                Vector2 aimDir = (primary.predicted - playerPos).normalized;
                // Nozzle position is along the aim ray from player
                Vector2 nozzlePos = playerPos + aimDir * nozzleOffset;
                float totalDamage = CalculateSprayDamage(
                    enemies,
                    nozzlePos,
                    aimDir,
                    halfAngle,
                    sprayRange
                );

                if (totalDamage > bestTotalDamage)
                {
                    bestTotalDamage = totalDamage;
                    bestTarget = primary.t;
                }
            }

            // Also sample directions BETWEEN enemy pairs (cluster centers)
            for (int i = 0; i < enemies.Count && i < 5; i++)
            {
                for (int j = i + 1; j < enemies.Count && j < 5; j++)
                {
                    Vector2 midpoint = (enemies[i].predicted + enemies[j].predicted) * 0.5f;
                    Vector2 aimDir = (midpoint - playerPos).normalized;
                    Vector2 nozzlePos = playerPos + aimDir * nozzleOffset;
                    float totalDamage = CalculateSprayDamage(
                        enemies,
                        nozzlePos,
                        aimDir,
                        halfAngle,
                        sprayRange
                    );

                    if (totalDamage > bestTotalDamage)
                    {
                        bestTotalDamage = totalDamage;
                        bestTarget =
                            enemies[i].dist < enemies[j].dist ? enemies[i].t : enemies[j].t;
                    }
                }
            }

            return bestTarget;
        }

        private static bool CanShootTarget(Collider target, Vector2 shooterPosition)
        {
            if (target == null)
                return false;

            Vector3 origin = shooterPosition.ToWorld(ProjectileVisualHeight);
            Vector3 targetPoint = target.bounds.center;
            targetPoint.y = ProjectileVisualHeight;
            return ProjectileWallCollision.HasClearLine(origin, targetPoint);
        }

        /// <summary>
        /// Calculate predicted enemy position using dynamic velocity-based prediction.
        /// - Stationary enemies: no prediction (aim dead center)
        /// - Close-range enemies: no prediction (aim dead center)
        /// - Moving enemies: prediction scales with velocity
        /// </summary>
        private Vector2 GetPredictedEnemyPosition(
            EnemyBase enemy,
            Vector2 currentPos,
            float distance,
            float particleSpeed
        )
        {
            // Close-range: aim dead center (no prediction)
            if (distance < SpraySettings.CloseRangeThreshold)
                return currentPos;

            // No rigidbody or stationary: aim dead center
            if (enemy.rb == null)
                return currentPos;

            Vector2 velocity = enemy.rb.GroundVelocity();
            float enemySpeed = velocity.magnitude;

            // Stationary or nearly stationary: aim dead center
            if (enemySpeed < 0.5f)
                return currentPos;

            // Dynamic prediction: scale with enemy speed
            // At reference speed, use full base prediction time
            // Faster enemies get more prediction, slower get less
            float speedRatio = enemySpeed / SpraySettings.PredictionReferenceSpeed;
            float predictionTime = SpraySettings.BasePredictionTime * speedRatio;

            // Also factor in particle travel time for very fast enemies
            float travelTime = distance / particleSpeed;
            predictionTime = Mathf.Min(
                predictionTime + travelTime * 0.5f,
                SpraySettings.MaxPredictionTime
            );

            return currentPos + velocity * predictionTime;
        }
    }
}
