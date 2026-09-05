using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
        /// <summary>
        /// Turns a direction the agent wants to go into one it can actually walk,
        /// by probing a fan of alternatives and taking the best compromise between
        /// pointing the right way and having room to move.
        ///
        /// The score carries a bias toward whichever way it went last tick. Without
        /// one, two comparable ways around a wall are settled by a tie-break, and
        /// the agent alternates between them on the spot -- pacing the wall rather
        /// than getting round it, which is the shape most of a wasted run takes.
        /// </summary>
        private Vector2 NavigateLocal(Vector2 position, Vector2 desired)
        {
            if (movement != null)
                return NavigatePhysical(position, desired, false);
            if (desired.sqrMagnitude < 0.0001f)
                return Vector2.zero;

            Vector2 normalized = desired.normalized;
            Vector2 best = Vector2.zero;
            float bestScore = float.NegativeInfinity;
            float openness = OpennessWeight(currentIntent, NearbyEnemyCount);
            for (int i = 0; i < AvoidanceAngles.Length; i++)
            {
                Vector2 candidate = Quaternion.Euler(0f, 0f, AvoidanceAngles[i]) * normalized;
                // Validate the step toward the corner, not three metres beyond
                // a turn the route has not reached yet.
                if (
                    !TryMeasureStepClearance(
                        position,
                        candidate,
                        Mathf.Clamp(desired.magnitude, 0.3f, 0.75f),
                        out float clearance
                    )
                )
                    continue;

                float score =
                    ScoreHeading(candidate, normalized, committedHeading, clearance, openness)
                    - i * 0.01f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (bestScore > float.NegativeInfinity)
            {
                blockedTicks = 0;
                committedHeading = best;
                return best;
            }

            // Nothing in the fan is walkable. Backing out the way it came in is the
            // right first answer, but on its own it is only half a manoeuvre: the
            // route is recomputed a sixth of a second later and points back into the
            // same geometry, so the agent shuffles in and out of the corner it is
            // wedged in without the stationary check ever quite firing. After a few
            // ticks of finding nowhere to go, head for the middle of the room
            // instead -- the one point in a room that is always on the navmesh.
            if (++blockedTicks >= BlockedTicksBeforeUnwedging)
            {
                blockedTicks = 0;
                unwedgeUntil = Time.time + unwedgeSeconds;
                nextPathRefresh = 0f;
            }

            committedHeading = -normalized;
            return committedHeading;
        }

        /// <summary>
        /// What one candidate direction is worth: mostly how close it is to where
        /// the agent wants to go and how much room it has, plus a nudge for
        /// continuing the way it was already going.
        /// </summary>
        /// <summary>
        /// How much room to move is worth against pointing the right way. Enough to
        /// bow the agent off a wall it would otherwise scrape along, and not enough to
        /// out-vote the route: a doorway is a gap one body wide, and an agent that
        /// preferred open ground over its destination stopped using them at all --
        /// measured as a run that reached ten rooms and then spent six game-minutes
        /// wandering the ones it had already seen.
        /// </summary>
        internal static float OpennessWeight(BotIntent intent, int nearbyEnemies) =>
            nearbyEnemies > 0
            && (
                intent == BotIntent.Engage
                || intent == BotIntent.Retreat
                || intent == BotIntent.Dodge
            )
                ? OpenGroundWeight
                : 1f;

        internal static float ScoreHeading(
            Vector2 candidate,
            Vector2 desired,
            Vector2 committed,
            float clearance,
            float clearanceWeight = 1f
        )
        {
            float score = Vector2.Dot(candidate, desired) * 2f + clearance * clearanceWeight;
            if (committed.sqrMagnitude < 0.0001f)
                return score;
            return score + Vector2.Dot(candidate, committed) * HeadingCommitment;
        }

        private bool TryMeasureClearance(
            Vector2 position,
            Vector2 direction,
            out float clearance
        ) => TryMeasureStepClearance(position, direction, navigationLookAhead, out clearance);

        private bool TryMeasureStepClearance(
            Vector2 position,
            Vector2 direction,
            float lookAhead,
            out float clearance
        )
        {
            // Far enough to tell open floor from a wall the agent is about to be
            // pinned against. At the old distance a wall a stride and a half away
            // measured as clear as the middle of the room.
            const float probeDistance = 3.2f;

            // The body's own width, and no wider. A fatter sweep reads a doorway as
            // impassable -- the arch is a tile across and the sides are inside the
            // sweep from the moment the agent lines up with it -- and an agent that
            // cannot use doorways stops exploring: measured as a run pinned in five
            // rooms with fifty unsticking manoeuvres to show for it.
            const float probeRadius = 0.42f;
            clearance = probeDistance;
            Vector3 origin = position.ToWorld(0.75f);
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                probeRadius,
                direction.ToWorld(),
                obstacleHits,
                probeDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = obstacleHits[i];
                if (!IsNavigationObstacle(hit.collider))
                    continue;
                clearance = Mathf.Min(clearance, hit.distance);
            }

            if (clearance < 0.2f)
                return false;

            if (NavMesh.SamplePosition(origin, out NavMeshHit fromHit, 1.5f, NavMesh.AllAreas))
            {
                Vector3 requested = (position + direction * lookAhead).ToWorld();
                if (
                    !NavMesh.SamplePosition(requested, out NavMeshHit toHit, 0.8f, NavMesh.AllAreas)
                    || NavMesh.Raycast(fromHit.position, toHit.position, out _, NavMesh.AllAreas)
                )
                    return false;
            }

            clearance /= probeDistance;
            return true;
        }

        private bool IsNavigationObstacle(Collider candidate)
        {
            if (candidate == null || candidate.transform.IsChildOf(player))
                return false;
            if (candidate.GetComponentInParent<EnemyBase>() != null)
                return false;
            if (candidate.GetComponentInParent<EnemyProjectile>() != null)
                return false;
            return true;
        }
    }
}
