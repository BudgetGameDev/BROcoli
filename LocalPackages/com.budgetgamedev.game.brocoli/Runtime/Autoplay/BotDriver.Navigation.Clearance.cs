using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
        /// <summary>
        /// How much room to move is worth against pointing the right way. Enough to
        /// bow the agent off a wall it would otherwise scrape along, and not enough to
        /// out-vote the route: a doorway is a gap one body wide, and an agent that
        /// preferred open ground over its destination stopped using them at all --
        /// measured as a run that reached ten rooms and then spent six game-minutes
        /// wandering the ones it had already seen.
        /// </summary>
        private float OpennessWeight() => NearbyEnemyCount > 0 ? OpenGroundWeight : 1f;

        /// <summary>
        /// What one candidate direction is worth: mostly how close it is to where
        /// the agent wants to go and how much room it has, plus a nudge for
        /// continuing the way it was already going.
        /// </summary>
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

        private bool TryMeasureClearance(Vector2 position, Vector2 direction, out float clearance)
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
                Vector3 requested = (position + direction * navigationLookAhead).ToWorld();
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
