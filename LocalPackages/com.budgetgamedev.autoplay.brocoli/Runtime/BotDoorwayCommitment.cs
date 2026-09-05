using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>A brief commitment to finish crossing a doorway before reconsidering optional goals.</summary>
    internal sealed class BotDoorwayCommitment
    {
        private Vector2Int from;
        private Vector2Int to;
        private bool attempted;
        private bool active;
        private float until;
        private const float DoorwayBand = 2f;
        private const float MaximumCommitment = 2f;

        internal void Begin(Vector2 position, Vector2Int origin, Vector2Int target, float time)
        {
            if (active || (attempted && origin == from && target == to))
                return;
            Vector2Int direction = target - origin;
            if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
                return;
            Vector2 midpoint =
                (DungeonLayout.RoomCenter(origin) + DungeonLayout.RoomCenter(target)) * 0.5f;
            float beforeDoor = Vector2.Dot(midpoint - position, direction);
            if (beforeDoor < 0f || beforeDoor > DoorwayBand)
                return;
            from = origin;
            to = target;
            attempted = active = true;
            until = time + MaximumCommitment;
        }

        internal bool TryGoal(Vector2 position, float time, out Vector2 goal)
        {
            goal = DungeonLayout.RoomCenter(to);
            Vector2 midpoint = (DungeonLayout.RoomCenter(from) + goal) * 0.5f;
            float entered = Vector2.Dot(position - midpoint, to - from);
            if (time >= until || entered >= DoorwayBand)
                active = false;
            return active;
        }

        internal BotIntent Resolve(
            BotIntent proposed,
            BotSituation situation,
            BotTuning tuning,
            Vector2 position,
            float time
        )
        {
            if (!TryGoal(position, time, out _))
                return proposed;
            bool urgent =
                situation.IncomingProjectile
                || (
                    situation.HasEnemies
                    && (
                        situation.NearestEnemyDistance < tuning.DangerRadius
                        || situation.CloseEnemyCount >= tuning.CrowdCount
                        || situation.Encirclement >= BotDecisionPolicy.CrowdingConcern
                    )
                );
            return urgent || proposed == BotIntent.Recover ? proposed : BotIntent.Explore;
        }

        internal void Clear()
        {
            attempted = active = false;
        }
    }
}
