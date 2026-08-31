using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        private void BuildShrine(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            string offering = (archetype.Variant % 3) switch
            {
                0 => DungeonPropTokens.Potion,
                1 => DungeonPropTokens.Coin,
                _ => DungeonPropTokens.Key,
            };
            PlaceNamed(
                parent,
                center,
                offering,
                Vector2.zero,
                archetype.Variant * 90f,
                occupied,
                1f
            );

            // Diablo-like shrines read as small ritual sites rather than a lone
            // pickup in an otherwise empty room: scattered offerings and a tight
            // devotional cluster frame the centre while the cross-shaped travel
            // lanes remain clear.
            Scatter(
                parent,
                center,
                archetype,
                random,
                occupied,
                4 + random.Next(0, 3),
                DungeonPropTokens.Pot,
                DungeonPropTokens.Potion,
                DungeonPropTokens.Coin,
                DungeonPropTokens.Stones
            );
            PlaceSmallClusters(
                parent,
                center,
                archetype,
                random,
                occupied,
                1,
                3,
                5,
                DungeonPropTokens.Pot,
                DungeonPropTokens.Coin,
                DungeonPropTokens.Potion
            );
        }

        private void BuildArena(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            Scatter(
                parent,
                center,
                archetype,
                random,
                occupied,
                5 + random.Next(0, 4),
                DungeonPropTokens.Stones,
                DungeonPropTokens.WeaponSpear,
                DungeonPropTokens.WeaponSword,
                DungeonPropTokens.ShieldRound
            );
            PlaceSmallClusters(
                parent,
                center,
                archetype,
                random,
                occupied,
                2,
                3,
                5,
                DungeonPropTokens.Barrel,
                DungeonPropTokens.Pot,
                DungeonPropTokens.Stones
            );
        }
    }
}
