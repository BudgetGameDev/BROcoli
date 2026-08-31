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
            if (archetype.Environment == DungeonLayout.EnvironmentTheme.Cave)
                Scatter(
                    parent,
                    center,
                    archetype,
                    random,
                    occupied,
                    6 + random.Next(0, 4),
                    DungeonPropTokens.Rocks,
                    DungeonPropTokens.Stones
                );
            else
                Scatter(
                    parent,
                    center,
                    archetype,
                    random,
                    occupied,
                    5 + random.Next(0, 4),
                    DungeonPropTokens.WoodSupport,
                    DungeonPropTokens.WoodStructure,
                    DungeonPropTokens.Pot
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
                archetype.Environment == DungeonLayout.EnvironmentTheme.Cave
                    ? DungeonPropTokens.Stones
                    : DungeonPropTokens.Barrel,
                DungeonPropTokens.Pot
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
            string terrain =
                archetype.Environment == DungeonLayout.EnvironmentTheme.Cave
                    ? DungeonPropTokens.Rocks
                    : DungeonPropTokens.WoodSupport;
            string debris =
                archetype.Environment == DungeonLayout.EnvironmentTheme.Cave
                    ? DungeonPropTokens.Stones
                    : DungeonPropTokens.Barrel;
            Scatter(
                parent,
                center,
                archetype,
                random,
                occupied,
                4 + random.Next(0, 3),
                terrain,
                debris,
                DungeonPropTokens.Pot
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
                debris,
                DungeonPropTokens.Pot
            );
        }
    }
}
