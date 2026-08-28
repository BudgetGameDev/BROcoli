using UnityEngine;

public partial class DungeonRoomBuilder
{
    /// <summary>
    /// Builds one shared wall run between two rooms. Open runs can contain one
    /// to three bare gaps, archways, or a mixture; blocked runs retain a barred
    /// central gateway. Both neighbouring rooms share this geometry.
    /// </summary>
    public GameObject BuildEdge(Transform parent, DungeonEdge edge, DungeonPassage passage)
    {
        GameObject root = new GameObject(
            $"Edge ({edge.X}, {edge.Y}, {(edge.Horizontal ? "H" : "V")})"
        );
        root.transform.SetParent(parent, false);
        Transform wallRun = CreateOcclusionSection(root.transform, "Wall Run");
        DungeonOcclusionSection wallRunSection = wallRun.GetComponent<DungeonOcclusionSection>();
        Transform gatewayRoot = new GameObject("Gateways").transform;
        gatewayRoot.SetParent(wallRun, false);

        Vector2 roomCenter = DungeonLayout.RoomCenter(new Vector2Int(edge.X, edge.Y));
        if (edge.Horizontal)
            BuildHorizontalEdge(wallRun, gatewayRoot, wallRunSection, roomCenter, passage);
        else
            BuildVerticalEdge(wallRun, gatewayRoot, wallRunSection, roomCenter, passage);

        if (gatewayRoot.childCount > 0)
            ConfigureGatewayOcclusion(gatewayRoot, wallRun);
        else
            Destroy(gatewayRoot.gameObject);
        return root;
    }

    private void BuildHorizontalEdge(
        Transform wallRun,
        Transform gatewayRoot,
        DungeonOcclusionSection section,
        Vector2 roomCenter,
        DungeonPassage passage
    )
    {
        float boundaryZ = roomCenter.y + HalfRoomDepth;
        section.ConfigureEdge(
            new Vector3(roomCenter.x - HalfRoomWidth, 0f, boundaryZ),
            new Vector3(roomCenter.x + HalfRoomWidth, 0f, boundaryZ)
        );
        int centerIndex = DungeonLayout.RoomTilesX / 2;
        for (int i = 0; i < DungeonLayout.RoomTilesX; i++)
        {
            float x = roomCenter.x + (i - centerIndex) * Tile;
            if (passage.HasOpening(i))
            {
                BuildGateway(
                    gatewayRoot,
                    passage,
                    i,
                    new Vector3(x, 0f, boundaryZ + WallSlabCenterOffset),
                    Quaternion.identity
                );
                continue;
            }

            GameObject wall = Instantiate(
                wallPrefab,
                new Vector3(x, 0f, boundaryZ),
                Quaternion.identity,
                wallRun
            );
            // Move each horizontal seam to the far edge of the perpendicular
            // wall so regular wall pieces form a clean junction without posts.
            if (i == 0)
                ResizeWallAtBoundary(wall, -1f, false);
            else if (i == DungeonLayout.RoomTilesX - 1)
                ResizeWallAtBoundary(wall, 1f, true);
        }
    }

    private void BuildVerticalEdge(
        Transform wallRun,
        Transform gatewayRoot,
        DungeonOcclusionSection section,
        Vector2 roomCenter,
        DungeonPassage passage
    )
    {
        float boundaryX = roomCenter.x + HalfRoomWidth;
        section.ConfigureEdge(
            new Vector3(boundaryX, 0f, roomCenter.y - HalfRoomDepth),
            new Vector3(boundaryX, 0f, roomCenter.y + HalfRoomDepth)
        );
        int centerIndex = DungeonLayout.RoomTilesZ / 2;
        Quaternion sideways = Quaternion.Euler(0f, 90f, 0f);
        for (int j = 0; j < DungeonLayout.RoomTilesZ; j++)
        {
            float z = roomCenter.y + (j - centerIndex) * Tile;
            if (passage.HasOpening(j))
            {
                BuildGateway(
                    gatewayRoot,
                    passage,
                    j,
                    new Vector3(boundaryX + WallSlabCenterOffset, 0f, z),
                    sideways
                );
                continue;
            }

            Instantiate(wallPrefab, new Vector3(boundaryX, 0f, z), sideways, wallRun);
        }
    }

    private void BuildGateway(
        Transform gatewayRoot,
        DungeonPassage passage,
        int slot,
        Vector3 position,
        Quaternion rotation
    )
    {
        if (passage.Open && !passage.HasArchway(slot))
            return;

        GameObject prefab = passage.Open ? gateOpenPrefab : gateBlockedPrefab;
        if (prefab != null)
            Instantiate(prefab, position, rotation, gatewayRoot);
    }

    private static void ConfigureGatewayOcclusion(Transform gatewayRoot, Transform section)
    {
        DungeonOcclusionSection occlusionSection = section.GetComponent<DungeonOcclusionSection>();
        occlusionSection.ConfigureGateway(gatewayRoot);

        foreach (Transform gate in gatewayRoot)
        {
            var volume = new GameObject("Gateway Top Occlusion Volume");
            volume.transform.SetParent(gate, false);
            int wallLayer = LayerMask.NameToLayer("Wall");
            volume.layer = wallLayer >= 0 ? wallLayer : gate.gameObject.layer;
            volume
                .AddComponent<DungeonOcclusionVolume>()
                .Configure(new Vector3(0f, 2.15f, 0f), new Vector3(3.1f, 1f, 2f));
        }
    }
}
