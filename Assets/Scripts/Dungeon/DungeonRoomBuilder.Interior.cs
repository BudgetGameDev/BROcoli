using System.Collections.Generic;
using UnityEngine;

public partial class DungeonRoomBuilder
{
    private readonly List<DungeonWallPiece> interiorWalls = new();
    private readonly Dictionary<string, Transform> sections = new();

    /// <summary>
    /// Instantiates a planned set of wall pieces, grouping them into occlusion
    /// sections. Runs that touch are one freestanding structure and share a
    /// section, so a cross or a T lowers every arm at once instead of dropping
    /// the run the camera happened to hit and leaving the other standing.
    /// </summary>
    private void InstantiateWallRuns(Transform parent, List<DungeonWallPiece> walls)
    {
        Dictionary<string, string> groups = DungeonWallGrouping.ResolveInteriorGroups(walls);
        sections.Clear();
        foreach (DungeonWallPiece piece in walls)
            InstantiateWall(SectionFor(parent, groups, piece.Section), piece);
        sections.Clear();
    }

    private Transform SectionFor(
        Transform parent,
        Dictionary<string, string> groups,
        string section
    )
    {
        string group = groups.TryGetValue(section, out string resolved) ? resolved : section;
        if (!sections.TryGetValue(group, out Transform existing))
        {
            existing = CreateOcclusionSection(parent, group);
            sections[group] = existing;
        }
        return existing;
    }

    private static Transform CreateOcclusionSection(Transform parent, string name)
    {
        var section = new GameObject($"Occlusion Section - {name}");
        section.transform.SetParent(parent, false);
        section.AddComponent<DungeonOcclusionSection>();
        return section.transform;
    }
}
