using System.Collections.Generic;
using UnityEngine;

public partial class DungeonRoomBuilder
{
    private readonly List<DungeonWallPiece> interiorWalls = new();
    private readonly List<DungeonJunctionPost> interiorPosts = new();
    private readonly Dictionary<string, Transform> sections = new();

    /// <summary>
    /// Instantiates a planned set of wall pieces, grouping them into occlusion
    /// sections. Runs that touch are one freestanding structure and share a
    /// section, so a cross or a T lowers every arm at once instead of dropping
    /// the run the camera happened to hit and leaving the other standing. The
    /// posts capping their junctions join the same section for the same reason.
    /// </summary>
    private void InstantiateWallRuns(
        Transform parent,
        List<DungeonWallPiece> walls,
        List<DungeonJunctionPost> posts
    )
    {
        Dictionary<string, string> groups = DungeonWallGrouping.ResolveInteriorGroups(walls);
        sections.Clear();
        foreach (DungeonWallPiece piece in walls)
            InstantiateWall(SectionFor(parent, groups, piece.Section), piece);
        foreach (DungeonJunctionPost post in posts)
            InstantiateJunctionPost(SectionFor(parent, groups, post.Section), post);
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

    /// <summary>
    /// Stands a column on a junction so the two runs crossing there stop
    /// showing each other's interior. The post is architecture the camera has
    /// to reckon with but not something the player can walk into: the runs it
    /// caps already seal the corner, and a solid post would only put a lip at
    /// every corner to catch on.
    ///
    /// So its collider is left as a thing scene queries can find and nothing
    /// can touch. The occlusion pass raycasts for what stands in front of the
    /// player and still finds it, while the trigger flag keeps it out of the
    /// movement sweep and excluding every layer keeps it from reporting contact
    /// with anyone - without which walking past a corner would fire the
    /// player's trigger handler on every post in the dungeon.
    /// </summary>
    private void InstantiateJunctionPost(Transform parent, DungeonJunctionPost post)
    {
        if (junctionPostPrefab == null)
            return;

        GameObject built = Instantiate(
            junctionPostPrefab,
            post.Position.ToWorld(),
            Quaternion.identity,
            parent
        );
        foreach (Collider collider in built.GetComponentsInChildren<Collider>())
        {
            collider.isTrigger = true;
            collider.excludeLayers = ~0;
        }
    }

    private static Transform CreateOcclusionSection(Transform parent, string name)
    {
        var section = new GameObject($"Occlusion Section - {name}");
        section.transform.SetParent(parent, false);
        section.AddComponent<DungeonOcclusionSection>();
        return section.transform;
    }
}
