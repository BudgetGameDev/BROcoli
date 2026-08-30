using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// The world convention: gameplay happens on the XZ ground plane and +Y is up.
    /// Gameplay logic keeps using Vector2 "ground" coordinates, where ground.x is
    /// world X and ground.y is world Z. These helpers convert between ground and
    /// world space and emulate the old 2D-physics queries with 3D physics by
    /// treating every query as an infinite vertical column through the ground plane.
    /// </summary>
    public static class GroundPlane
    {
        /// <summary>Vertical span colliders occupy (see the prefab colliders).</summary>
        public const float ColliderBottom = -0.5f;
        public const float ColliderTop = 1.5f;

        // Query capsules span more than the collider band so nothing is missed.
        private const float QueryBottom = -2f;
        private const float QueryTop = 3f;

        /// <summary>Projects a world position onto the ground plane.</summary>
        public static Vector2 ToGround(this Vector3 world)
        {
            return new Vector2(world.x, world.z);
        }

        /// <summary>Lifts a ground position into world space at the given height.</summary>
        public static Vector3 ToWorld(this Vector2 ground, float height = 0f)
        {
            return new Vector3(ground.x, height, ground.y);
        }

        /// <summary>Ground-plane distance between two world positions.</summary>
        public static float GroundDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(a.ToGround(), b.ToGround());
        }

        /// <summary>A yaw rotation that turns ground +X toward the given ground angle.
        /// Equivalent of the old Quaternion.Euler(0, 0, degrees) facing rotation.</summary>
        public static Quaternion YawRotation(float degrees)
        {
            return Quaternion.Euler(0f, -degrees, 0f);
        }

        /// <summary>Reads back the angle written by <see cref="YawRotation"/>.</summary>
        public static float YawDegrees(Transform transform)
        {
            return -transform.localEulerAngles.y;
        }

        // ==================== Rigidbody ground helpers ====================

        public static Vector2 GroundPosition(this Rigidbody body)
        {
            return body.position.ToGround();
        }

        public static void MoveGroundPosition(this Rigidbody body, Vector2 ground)
        {
            body.MovePosition(new Vector3(ground.x, body.position.y, ground.y));
        }

        public static void SetGroundPosition(this Rigidbody body, Vector2 ground)
        {
            body.position = new Vector3(ground.x, body.position.y, ground.y);
        }

        public static Vector2 GroundVelocity(this Rigidbody body)
        {
            return body.linearVelocity.ToGround();
        }

        public static void SetGroundVelocity(this Rigidbody body, Vector2 velocity)
        {
            Vector3 current = body.linearVelocity;
            body.linearVelocity = new Vector3(velocity.x, current.y, velocity.y);
        }

        /// <summary>Equivalent of the old Rigidbody2D.simulated toggle used by pooling
        /// and death handling: stops the body from moving or being hit.</summary>
        public static void SetSimulated(this Rigidbody body, bool simulated)
        {
            if (!simulated && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.detectCollisions = simulated;
        }

        // ==================== 2D-equivalent physics queries ====================

        /// <summary>Overlap a circle on the ground plane (a vertical capsule in 3D).</summary>
        public static int OverlapCircle(
            Vector2 center,
            float radius,
            Collider[] results,
            int layerMask = Physics.AllLayers,
            QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide
        )
        {
            return Physics.OverlapCapsuleNonAlloc(
                center.ToWorld(QueryBottom),
                center.ToWorld(QueryTop),
                radius,
                results,
                layerMask,
                triggers
            );
        }

        /// <summary>Allocating variant of <see cref="OverlapCircle"/>.</summary>
        public static Collider[] OverlapCircleAll(
            Vector2 center,
            float radius,
            int layerMask = Physics.AllLayers,
            QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide
        )
        {
            return Physics.OverlapCapsule(
                center.ToWorld(QueryBottom),
                center.ToWorld(QueryTop),
                radius,
                layerMask,
                triggers
            );
        }

        /// <summary>Finds a collider containing the given world point, projected to the
        /// ground plane. Equivalent of the old Physics2D.OverlapPoint.</summary>
        public static Collider OverlapPoint(
            Vector3 worldPoint,
            int layerMask = Physics.AllLayers,
            QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide
        )
        {
            Collider[] hits = Physics.OverlapCapsule(
                worldPoint.ToGround().ToWorld(QueryBottom),
                worldPoint.ToGround().ToWorld(QueryTop),
                0.01f,
                layerMask,
                triggers
            );
            return hits.Length > 0 ? hits[0] : null;
        }

        /// <summary>Edge-to-edge ground gap between two convex colliders. Equivalent of
        /// the old Collider2D.Distance distance. Returns 0 when overlapped.</summary>
        public static float ColliderGap(Collider a, Collider b)
        {
            if (a == null || b == null)
                return float.PositiveInfinity;

            Vector3 aCenter = a.bounds.center;
            Vector3 onB = b.ClosestPoint(aCenter);
            if ((onB - aCenter).sqrMagnitude < 0.000001f)
                return 0f; // a's center is inside b
            Vector3 onA = a.ClosestPoint(onB);
            if ((onA - onB).sqrMagnitude < 0.000001f)
                return 0f; // surfaces touch or overlap
            return Vector2.Distance(onA.ToGround(), onB.ToGround());
        }
    }
}
