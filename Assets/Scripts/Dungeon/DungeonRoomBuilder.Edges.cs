using System.Collections.Generic;
using UnityEngine;

public partial class DungeonRoomBuilder
{
    private readonly List<DungeonWallPiece> edgeWalls = new();
    private readonly List<DungeonArchway> edgeArchways = new();
    private readonly List<DungeonJunctionPost> edgePosts = new();

    /// <summary>
    /// Builds one shared wall run between two rooms from its planned geometry.
    /// An open run drops one to three wall pieces to form doorways, at most one
    /// of which is framed by an archway; a closed run is an unbroken wall. Both
    /// neighbouring rooms share this geometry.
    /// </summary>
    public GameObject BuildEdge(Transform parent, DungeonEdge edge, DungeonPassage passage)
    {
        GameObject root = new GameObject(
            $"Edge ({edge.X}, {edge.Y}, {(edge.Horizontal ? "H" : "V")})"
        );
        root.transform.SetParent(parent, false);

        Transform wallRun = CreateOcclusionSection(root.transform, "Wall Run");
        edgeWalls.Clear();
        DungeonRoomGeometry.AppendEdgeWalls(edgeWalls, edge, passage);
        foreach (DungeonWallPiece piece in edgeWalls)
            InstantiateWall(wallRun, piece);

        BuildArchways(wallRun, edge, passage);
        BuildJunctionPosts(root.transform, edge);
        return root;
    }

    /// <summary>
    /// Caps the grid post this run ends at. It sits outside the run's occlusion
    /// section on purpose: the two runs meeting at a post are kept apart so a
    /// room's south wall never drops its east wall, and a post that belongs to
    /// both is judged on its own instead of being dragged down by either.
    /// </summary>
    private void BuildJunctionPosts(Transform parent, DungeonEdge edge)
    {
        edgePosts.Clear();
        DungeonRoomGeometry.AppendEdgeJunctions(edgePosts, edge);
        foreach (DungeonJunctionPost post in edgePosts)
            InstantiateJunctionPost(parent, post);
    }

    private void BuildArchways(Transform wallRun, DungeonEdge edge, DungeonPassage passage)
    {
        edgeArchways.Clear();
        DungeonRoomGeometry.AppendEdgeArchways(edgeArchways, edge, passage);
        if (edgeArchways.Count == 0 || gateOpenPrefab == null)
            return;

        Transform gatewayRoot = new GameObject("Gateways").transform;
        gatewayRoot.SetParent(wallRun, false);
        foreach (DungeonArchway archway in edgeArchways)
        {
            Instantiate(
                gateOpenPrefab,
                archway.Position.ToWorld(DungeonArchway.BaseLift),
                Quaternion.Euler(0f, archway.Yaw, 0f),
                gatewayRoot
            );
        }
        ConfigureGatewayOcclusion(gatewayRoot, wallRun);
    }

    /// <summary>Instantiates one planned wall piece on its slab centre line.</summary>
    private void InstantiateWall(Transform parent, DungeonWallPiece piece)
    {
        Instantiate(
            wallPrefab,
            piece.PrefabPosition.ToWorld(piece.BaseLift),
            piece.AlongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f),
            parent
        );
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
                .Configure(
                    DungeonArchway.OcclusionVolumeCenter,
                    DungeonArchway.OcclusionVolumeSize
                );
        }
    }
}
