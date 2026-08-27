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
        for (int i = -3; i <= 3; i++)
        {
            if (leaveCentreGap && i == 0)
                continue;
            Instantiate(
                wallPrefab,
                new Vector3(center.x + i * Tile, 0f, center.y + localZ),
                Quaternion.identity,
                parent
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
        Quaternion sideways = Quaternion.Euler(0f, 90f, 0f);
        for (int j = -2; j <= 2; j++)
        {
            if (leaveCentreGap && j == 0)
                continue;
            Instantiate(
                wallPrefab,
                new Vector3(center.x + localX, 0f, center.y + j * Tile),
                sideways,
                parent
            );
        }
    }

    private void BuildVerticalDivider(Transform parent, Vector2 center)
    {
        Quaternion sideways = Quaternion.Euler(0f, 90f, 0f);
        foreach (int j in new[] { -1, 1 })
        {
            Instantiate(
                wallPrefab,
                new Vector3(center.x, 0f, center.y + j * Tile),
                sideways,
                parent
            );
        }
    }

    private void BuildHorizontalDivider(Transform parent, Vector2 center)
    {
        foreach (int i in new[] { -2, 0, 2 })
        {
            Instantiate(
                wallPrefab,
                new Vector3(center.x + i * Tile, 0f, center.y),
                Quaternion.identity,
                parent
            );
        }
    }
}
