using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
        private Vector2 NavigatePhysical(Vector2 position, Vector2 desired, bool followRoute)
        {
            AcceptedStep = Vector2.zero;
            MovementBlocked = false;
            if (desired.sqrMagnitude < 0.0001f)
                return Vector2.zero;
            lastRequestedDirection = desired.normalized;
            float step = Mathf.Max(
                0.01f,
                (stats != null ? stats.CurrentMovementSpeed : 4f) * Time.fixedDeltaTime
            );
            Vector2 direct = Vector2.ClampMagnitude(desired / step, 1f);
            if (
                followRoute
                && TryPhysicalStep(position, direct * step, out Vector2 advanced)
                && Vector2.Dot(advanced, direct.normalized) >= direct.magnitude * step * 0.9f
            )
            {
                AcceptedStep = advanced;
                committedHeading = direct.normalized;
                return direct;
            }

            float bestScore = float.NegativeInfinity;
            string bestStatus = "blocked";
            Vector2 bestInput = Vector2.zero;
            Vector2 heading = desired.normalized;
            for (int index = 0; index < AvoidanceAngles.Length; index++)
            {
                Vector2 candidate = Quaternion.Euler(0f, 0f, AvoidanceAngles[index]) * heading;
                if (!TryPhysicalStep(position, candidate * step, out Vector2 actual))
                    continue;
                // Score the displacement the player's capsule can actually make,
                // including enemy stand-off and wall sliding, not an unrelated sphere.
                float score = Vector2.Dot(actual / step, heading) * 2f + actual.magnitude / step;
                score -= index * 0.001f;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                bestInput = candidate;
                AcceptedStep = actual;
                bestStatus = StepStatus;
            }
            if (bestInput == Vector2.zero)
            {
                BlockedStepCount++;
                MovementBlocked = true;
                return Vector2.zero;
            }
            if (
                committedHeading.sqrMagnitude > 0f
                && TryPhysicalStep(position, committedHeading * step, out Vector2 continued)
            )
            {
                float continuedScore =
                    Vector2.Dot(continued / step, heading) * 2f + continued.magnitude / step;
                if (PreferContinuingHeading(continuedScore, bestScore))
                {
                    bestInput = committedHeading;
                    AcceptedStep = continued;
                    bestStatus = StepStatus;
                }
            }
            StepStatus = bestStatus;
            committedHeading = AcceptedStep.normalized;
            return bestInput;
        }

        private bool TryPhysicalStep(Vector2 position, Vector2 desired, out Vector2 actual)
        {
            actual = movement.PreviewNavigationDelta(desired);
            StepStatus = "physical-blocked";
            if (actual.sqrMagnitude < desired.sqrMagnitude * 0.01f)
                return false;
            Vector3 origin = position.ToWorld(player != null ? player.position.y : 0f);
            StepStatus = "no-source-navmesh";
            // Authored floor meshes bake above y=0. Separate vertical reach from
            // the horizontal constraint; a tiny 3D radius rejects valid floor tiles.
            if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 1.5f, NavMesh.AllAreas))
                return false;
            Vector3 endpoint = (position + actual).ToWorld(start.position.y);
            StepStatus = "no-target-navmesh";
            if (!NavMesh.SamplePosition(endpoint, out NavMeshHit end, 1.5f, NavMesh.AllAreas))
                return false;
            float startOffset = Vector2.Distance(start.position.ToGround(), position);
            float endOffset = Vector2.Distance(end.position.ToGround(), position + actual);
            StepStatus = "outward-off-mesh";
            // Outside the mesh, allow inward steps and wall tangents that do not
            // move farther out. An enemy can block the inward direction while a
            // legal tangent is the only way to reach a gap in that crowd.
            if (!AcceptsHorizontalProjection(startOffset, endOffset))
                return false;
            // A short physical step can cross a baked prop seam even when the
            // projected endpoints lie on disconnected polygons. The player's
            // capsule resolver owns collision; NavMesh remains route guidance.
            StepStatus = endOffset > 0.05f ? "reentering" : "accepted";
            return true;
        }

        internal static bool AcceptsHorizontalProjection(float before, float after) =>
            after <= 0.05f || (before > 0.05f && after <= before + 0.0001f);

        internal static bool PreferContinuingHeading(float continuedScore, float bestScore) =>
            continuedScore >= bestScore - 0.12f;
    }
}
