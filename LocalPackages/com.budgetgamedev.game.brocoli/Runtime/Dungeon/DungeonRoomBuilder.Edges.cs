using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonRoomBuilder
    {
        private readonly List<DungeonWallPiece> edgeWalls = new();
        private readonly List<DungeonArchway> edgeArchways = new();

        /// <summary>
        /// Builds one shared wall run between two rooms from its planned geometry.
        /// An open run drops one to three wall pieces to form doorways, at most one
        /// of which is framed by an archway; a closed run is an unbroken wall. Both
        /// neighbouring rooms share this geometry.
        /// </summary>
        public GameObject BuildEdge(
            Transform parent,
            DungeonEdge edge,
            DungeonPassage passage,
            DungeonEdgeStyle style = DungeonEdgeStyle.Interior
        )
        {
            GameObject root = new GameObject(
                $"Edge ({edge.X}, {edge.Y}, {(edge.Horizontal ? "H" : "V")}) [{style}]"
            );
            root.transform.SetParent(parent, false);

            if (style == DungeonEdgeStyle.SouthCliff)
            {
                BuildSouthCliff(root.transform, edge);
                return root;
            }

            if (style == DungeonEdgeStyle.OpenCrossing)
                return root;

            Transform wallRun = CreateOcclusionSection(root.transform, "Wall Run");
            wallRun.GetComponent<DungeonOcclusionSection>().ConfigureEdge(edge);
            edgeWalls.Clear();
            DungeonRoomGeometry.AppendEdgeWalls(edgeWalls, edge, passage);
            foreach (DungeonWallPiece piece in edgeWalls)
                InstantiateWall(wallRun, piece);

            BuildArchways(wallRun, edge, passage);
            return root;
        }

        /// <summary>
        /// Builds a camera-facing platform edge. A knee-high parapet provides the
        /// collision/readability of a railing without ever hiding a character;
        /// the same modular masonry continues below floor level as a cliff face.
        /// Two courses deep, the lower one stepped slightly outward, the drop
        /// reads as the battered flank of a tall platform rather than a table
        /// edge, so the playable floor sits in a larger vertical world.
        /// </summary>
        private void BuildSouthCliff(Transform parent, DungeonEdge edge)
        {
            const float parapetScale = 0.3f;
            const float cliffFaceScale = 1.5f;
            const float lowerCourseOutset = 0.45f;

            Transform parapet = new GameObject("Low South Parapet").transform;
            parapet.SetParent(parent, false);
            parapet.gameObject.AddComponent<DungeonContentRoot>();

            Transform cliffFace = new GameObject("Cliff Face Below Floor").transform;
            cliffFace.SetParent(parent, false);
            cliffFace.gameObject.AddComponent<DungeonContentRoot>();

            edgeWalls.Clear();
            DungeonRoomGeometry.AppendEdgeWalls(edgeWalls, edge, new DungeonPassage(false, 0, 0));
            foreach (DungeonWallPiece piece in edgeWalls)
            {
                InstantiateScaledWall(parapet, piece, parapetScale, piece.BaseLift);
                float upperCourseLift =
                    piece.BaseLift - DungeonWallPiece.SlabHeight * cliffFaceScale;
                InstantiateScaledWall(cliffFace, piece, cliffFaceScale, upperCourseLift);
                InstantiateScaledWall(
                    cliffFace,
                    piece,
                    cliffFaceScale,
                    upperCourseLift - DungeonWallPiece.SlabHeight * cliffFaceScale,
                    lowerCourseOutset
                );
            }
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

        private void InstantiateScaledWall(
            Transform parent,
            DungeonWallPiece piece,
            float verticalScale,
            float lift,
            float southOffset = 0f,
            string name = null
        )
        {
            GameObject wall = Instantiate(
                wallPrefab,
                (piece.PrefabPosition + Vector2.down * southOffset).ToWorld(lift),
                piece.AlongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f),
                parent
            );
            wall.name =
                name ?? (verticalScale < 1f ? "DungeonWall - Low Parapet" : "DungeonWall - Cliff");
            wall.transform.localScale = Vector3.Scale(
                wall.transform.localScale,
                new Vector3(1f, verticalScale, 1f)
            );
        }

        private static void ConfigureGatewayOcclusion(Transform gatewayRoot, Transform section)
        {
            DungeonOcclusionSection occlusionSection =
                section.GetComponent<DungeonOcclusionSection>();
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
}
