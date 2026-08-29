using System.Collections.Generic;
using UnityEngine;

public partial class DungeonRoomBuilder
{
    private readonly List<DungeonWallPiece> interiorWalls = new();
    private readonly Dictionary<string, Transform> sections = new();

    /// <summary>
    /// Instantiates a planned set of wall pieces, grouping them into occlusion
    /// sections so pieces on the same run fade together.
    /// </summary>
    private void InstantiateWallRuns(Transform parent, List<DungeonWallPiece> walls)
    {
        sections.Clear();
        foreach (DungeonWallPiece piece in walls)
        {
            if (!sections.TryGetValue(piece.Section, out Transform section))
            {
                section = CreateOcclusionSection(parent, piece.Section);
                sections[piece.Section] = section;
            }
            InstantiateWall(section, piece);
        }
        sections.Clear();
    }

    private static Transform CreateOcclusionSection(Transform parent, string name)
    {
        var section = new GameObject($"Occlusion Section - {name}");
        section.transform.SetParent(parent, false);
        section.AddComponent<DungeonOcclusionSection>();
        return section.transform;
    }
}
