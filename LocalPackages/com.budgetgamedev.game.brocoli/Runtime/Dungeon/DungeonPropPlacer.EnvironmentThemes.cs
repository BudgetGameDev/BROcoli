using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        private void BuildCollapsed(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            DungeonEnvironmentProfile profile = DungeonEnvironmentProfile.Of(
                archetype.Environment
            );
            Scatter(
                parent,
                center,
                archetype,
                random,
                occupied,
                4 + random.Next(0, 3),
                profile.RubbleTokens
            );
            PlaceSmallClusters(
                parent,
                center,
                archetype,
                random,
                occupied,
                1 + random.Next(0, 2),
                3,
                6,
                profile.ClutterTokens
            );
        }

        private void BuildFlooded(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            DungeonEnvironmentProfile profile = DungeonEnvironmentProfile.Of(
                archetype.Environment
            );
            Scatter(
                parent,
                center,
                archetype,
                random,
                occupied,
                4 + random.Next(0, 3),
                profile.RubbleTokens
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
                profile.ClutterTokens
            );
        }
    }
}
