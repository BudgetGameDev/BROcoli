using UnityEngine;

public partial class DungeonRoomBuilder
{
    private void BuildHorizontalInterior(
        Transform parent,
        Vector2 center,
        float localZ,
        bool leaveCentreGap
    )
    {
        Transform left = CreateOcclusionSection(parent, $"Horizontal {localZ:0.##} Left");
        Transform right = leaveCentreGap
            ? CreateOcclusionSection(parent, $"Horizontal {localZ:0.##} Right")
            : left;
        for (int i = -3; i <= 3; i++)
        {
            if (leaveCentreGap && i == 0)
                continue;
            GameObject wall = Instantiate(
                wallPrefab,
                new Vector3(center.x + i * Tile, 0f, center.y + localZ),
                Quaternion.identity,
                i < 0 ? left : right
            );
            if (i == -3 || i == 3)
                TrimBoundaryOverlap(wall, i < 0 ? 1f : -1f);
        }
    }

    private void BuildVerticalInterior(
        Transform parent,
        Vector2 center,
        float localX,
        bool leaveCentreGap
    )
    {
        Transform lower = CreateOcclusionSection(parent, $"Vertical {localX:0.##} Lower");
        Transform upper = leaveCentreGap
            ? CreateOcclusionSection(parent, $"Vertical {localX:0.##} Upper")
            : lower;
        Quaternion sideways = Quaternion.Euler(0f, 90f, 0f);
        for (int j = -2; j <= 2; j++)
        {
            if (leaveCentreGap && j == 0)
                continue;
            GameObject wall = Instantiate(
                wallPrefab,
                new Vector3(center.x + localX, 0f, center.y + j * Tile),
                sideways,
                j < 0 ? lower : upper
            );
            if (j == -2 || j == 2)
            {
                // A +90-degree wall maps local -X toward world +Z.
                TrimBoundaryOverlap(wall, j < 0 ? -1f : 1f);
            }
        }
    }

    private void BuildVerticalDivider(Transform parent, Vector2 center)
    {
        Quaternion sideways = Quaternion.Euler(0f, 90f, 0f);
        foreach (int j in new[] { -1, 1 })
        {
            Transform section = CreateOcclusionSection(
                parent,
                j < 0 ? "Vertical Divider Lower" : "Vertical Divider Upper"
            );
            Instantiate(
                wallPrefab,
                new Vector3(center.x, 0f, center.y + j * Tile),
                sideways,
                section
            );
        }
    }

    private void BuildHorizontalDivider(Transform parent, Vector2 center)
    {
        foreach (int i in new[] { -2, 0, 2 })
        {
            Transform section = CreateOcclusionSection(parent, $"Horizontal Divider {i:+#;-#;0}");
            Instantiate(
                wallPrefab,
                new Vector3(center.x + i * Tile, 0f, center.y),
                Quaternion.identity,
                section
            );
        }
    }

    private static Transform CreateOcclusionSection(Transform parent, string name)
    {
        var section = new GameObject($"Occlusion Section - {name}");
        section.transform.SetParent(parent, false);
        section.AddComponent<DungeonOcclusionSection>();
        return section.transform;
    }

    private static void TrimBoundaryOverlap(GameObject wall, float inwardLocalX)
    {
        ResizeWallAtBoundary(wall, -inwardLocalX, false);
    }

    private static void ResizeWallAtBoundary(GameObject wall, float outwardLocalX, bool extend)
    {
        MeshFilter meshFilter = wall.GetComponentInChildren<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return;

        Transform visual = meshFilter.transform;
        Bounds meshBounds = meshFilter.sharedMesh.bounds;
        float visualLength = meshBounds.size.x * Mathf.Abs(visual.localScale.x);
        float boundaryOverlap = meshBounds.extents.z * Mathf.Abs(visual.localScale.z);
        if (visualLength <= 0.0001f || boundaryOverlap <= 0f)
            return;

        // Move the endpoint by one visual half-depth so perpendicular wall
        // meshes intersect instead of exposing their beveled end faces. The
        // collider follows the same adjustment and keeps its authored 0.2-unit
        // overhang beyond the visible wall.
        float lengthAdjustment = extend ? boundaryOverlap : -boundaryOverlap;
        float adjustedLength = Mathf.Max(0.0001f, visualLength + lengthAdjustment);
        Vector3 scale = visual.localScale;
        scale.x *= adjustedLength / visualLength;
        visual.localScale = scale;

        Vector3 position = visual.localPosition;
        position.x += outwardLocalX * lengthAdjustment * 0.5f;
        visual.localPosition = position;

        BoxCollider wallCollider = wall.GetComponent<BoxCollider>();
        if (wallCollider == null)
            return;

        Vector3 colliderSize = wallCollider.size;
        colliderSize.x = Mathf.Max(0f, colliderSize.x + lengthAdjustment);
        wallCollider.size = colliderSize;

        Vector3 colliderCenter = wallCollider.center;
        colliderCenter.x += outwardLocalX * lengthAdjustment * 0.5f;
        wallCollider.center = colliderCenter;
    }
}
