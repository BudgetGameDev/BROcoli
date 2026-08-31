using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonManager
    {
        private void BuildRoomEdges(Vector2Int room)
        {
            for (int direction = 0; direction < 4; direction++)
            {
                DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
                if (loadedEdges.ContainsKey(edge))
                    continue;

                DungeonEdgeStyle style = layout.PlayableEdgeStyle(edge);
                DungeonLayout.EnvironmentTheme environment = layout.EnvironmentAt(edge);
                GameObject builtEdge = builder.BuildEdge(
                    transform,
                    edge,
                    layout.PlayablePassage(room, direction),
                    style,
                    environment
                );
                loadedEdges[edge] = builtEdge;
                if (style != DungeonEdgeStyle.SouthCliff && style != DungeonEdgeStyle.SolidBoundary)
                    continue;

                decor.BuildBoundaryDressing(
                    builtEdge.transform,
                    room,
                    direction,
                    environment,
                    layout.RoomRandom(new Vector2Int(edge.X, edge.Y), 1202)
                );
            }
        }
    }
}
