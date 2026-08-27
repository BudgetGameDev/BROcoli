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
            Instantiate(
                wallPrefab,
                new Vector3(center.x + i * Tile, 0f, center.y + localZ),
                Quaternion.identity,
                i < 0 ? left : right
            );
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
            Instantiate(
                wallPrefab,
                new Vector3(center.x + localX, 0f, center.y + j * Tile),
                sideways,
                j < 0 ? lower : upper
            );
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
}
