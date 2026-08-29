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

    /// <summary>
    /// Moves one end of a wall piece by the length the plan asked for. Runs
    /// that meet a perpendicular wall recede from it or push through it so
    /// their bevelled end faces never show at the junction.
    /// </summary>
    private static void ResizeWallEnd(GameObject wall, DungeonWallPiece piece)
    {
        MeshFilter meshFilter = wall.GetComponentInChildren<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return;

        Transform visual = meshFilter.transform;
        float visualLength = meshFilter.sharedMesh.bounds.size.x * Mathf.Abs(visual.localScale.x);
        if (visualLength <= 0.0001f)
            return;

        // A +90-degree wall maps local +X toward world -Z, so a run shift along
        // the piece's world axis flips sign for vertical runs.
        float adjustment = piece.LengthAdjustment;
        float localShift = piece.AlongX ? piece.RunShift : -piece.RunShift;

        Vector3 scale = visual.localScale;
        scale.x *= Mathf.Max(0.0001f, visualLength + adjustment) / visualLength;
        visual.localScale = scale;

        Vector3 position = visual.localPosition;
        position.x += localShift;
        visual.localPosition = position;

        BoxCollider wallCollider = wall.GetComponent<BoxCollider>();
        if (wallCollider == null)
            return;

        Vector3 colliderSize = wallCollider.size;
        colliderSize.x = Mathf.Max(0f, colliderSize.x + adjustment);
        wallCollider.size = colliderSize;

        Vector3 colliderCenter = wallCollider.center;
        colliderCenter.x += localShift;
        wallCollider.center = colliderCenter;
    }
}
