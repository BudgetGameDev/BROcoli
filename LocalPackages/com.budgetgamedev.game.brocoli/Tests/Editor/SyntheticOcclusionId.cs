using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Round-trips simulation indexes through opaque IDs without creating scene
    /// objects. These raw values are fixture data only, never Unity object IDs;
    /// no test resolves them to objects or relies on Unity's internal bit layout.
    /// </summary>
    internal static class SyntheticOcclusionId
    {
        public static EntityId FromIndex(int index)
        {
            return EntityId.FromULong(checked((ulong)index + 1));
        }

        public static int ToIndex(EntityId id)
        {
            return checked((int)(EntityId.ToULong(id) - 1));
        }
    }
}
