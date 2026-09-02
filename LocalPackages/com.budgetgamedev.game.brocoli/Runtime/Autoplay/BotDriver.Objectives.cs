using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
        /// <summary>
        /// The nearest thing on the floor worth walking to. Positions are captured at
        /// scan time and distances measured from wherever the agent is now, so a
        /// throttled scan still yields an up-to-date urgency.
        /// </summary>
        internal readonly struct ObjectiveObservation
        {
            internal readonly bool HasChest;
            internal readonly Vector2 Chest;
            internal readonly bool HasPickup;
            internal readonly Vector2 Pickup;

            internal static ObjectiveObservation None => default;

            internal ObjectiveObservation(
                bool hasChest,
                Vector2 chest,
                bool hasPickup,
                Vector2 pickup
            )
            {
                HasChest = hasChest;
                Chest = chest;
                HasPickup = hasPickup;
                Pickup = pickup;
            }

            internal float ChestDistance(Vector2 from) =>
                HasChest ? Vector2.Distance(from, Chest) : float.PositiveInfinity;

            internal float PickupDistance(Vector2 from) =>
                HasPickup ? Vector2.Distance(from, Pickup) : float.PositiveInfinity;
        }

        /// <summary>
        /// Sweeps for chests and loose pickups. A chest is the one reward the game
        /// never brings to the player, so an agent that does not deliberately walk
        /// into one leaves the whole loot path untested.
        /// </summary>
        private ObjectiveObservation ObserveObjectives(Vector2 position)
        {
            int count = GroundPlane.OverlapCircle(position, objectiveRadius, objectiveBuffer);
            var chest = new NearestTarget(position);
            var boost = new NearestTarget(position);
            var experience = new NearestTarget(position);

            for (int index = 0; index < count; index++)
                Classify(objectiveBuffer[index], ref chest, ref boost, ref experience);

            NearestTarget pickup = PreferredPickup(boost, experience);
            return new ObjectiveObservation(
                chest.Found,
                chest.Position,
                pickup.Found,
                pickup.Position
            );
        }

        /// <summary>Sorts one overlapping collider into the target it belongs to.</summary>
        internal static void Classify(
            Collider candidate,
            ref NearestTarget chest,
            ref NearestTarget boost,
            ref NearestTarget experience
        )
        {
            if (candidate == null)
                return;

            Vector2 spot = candidate.transform.position.ToGround();
            if (candidate.GetComponentInParent<LootChest>() != null)
                chest.Offer(spot);
            else if (candidate.GetComponentInParent<BoostBase>() != null)
                boost.Offer(spot);
            else if (candidate.GetComponentInParent<ExpGain>() != null)
                experience.Offer(spot);
        }

        /// <summary>
        /// Boosts outrank experience orbs: they are rarer, they expire, and several
        /// stat systems are only reachable by picking one up. Orbs get collected by
        /// the magnet in passing anyway.
        /// </summary>
        internal static NearestTarget PreferredPickup(
            NearestTarget boost,
            NearestTarget experience
        ) => boost.Found ? boost : experience;

        /// <summary>Running "closest so far" accumulator for one class of target.</summary>
        internal struct NearestTarget
        {
            private readonly Vector2 origin;
            private float best;

            internal bool Found { get; private set; }
            internal Vector2 Position { get; private set; }

            internal NearestTarget(Vector2 origin)
            {
                this.origin = origin;
                best = float.PositiveInfinity;
                Found = false;
                Position = origin;
            }

            internal void Offer(Vector2 candidate)
            {
                float distance = Vector2.Distance(origin, candidate);
                if (distance >= best)
                    return;

                best = distance;
                Position = candidate;
                Found = true;
            }
        }
    }
}
