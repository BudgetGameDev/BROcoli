using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonRoomBuilder
    {
        private readonly List<DungeonWallPiece> edgeWalls = new();
        private readonly List<DungeonArchway> edgeArchways = new();

        /// <summary>
        /// Builds one shared half-height railing run between two rooms from its
        /// planned geometry. An open run drops one to three pieces to form
        /// doorways, at most one of which is framed by an archway; a closed run is
        /// unbroken. Both neighbouring rooms share this geometry.
        /// <paramref name="parapetJoinMask"/> names the ends of the run that land
        /// on a corner where the platform's boundary parapet turns, so those
        /// pieces can be built to the parapet's height instead.
        /// </summary>
        public GameObject BuildEdge(
            Transform parent,
            DungeonEdge edge,
            DungeonPassage passage,
            DungeonEdgeStyle style = DungeonEdgeStyle.Interior,
            DungeonLayout.EnvironmentTheme environment = DungeonLayout.EnvironmentTheme.Dungeon,
            int parapetJoinMask = 0
        )
        {
            GameObject root = new GameObject(
                $"Edge ({edge.X}, {edge.Y}, {(edge.Horizontal ? "H" : "V")}) [{style}]"
            );
            root.transform.SetParent(parent, false);

            if (
                style == DungeonEdgeStyle.SouthCliff
                || style == DungeonEdgeStyle.SideCliff
                || style == DungeonEdgeStyle.SolidBoundary
            )
            {
                BuildEnvironmentBoundary(
                    root.transform,
                    edge,
                    environment,
                    style != DungeonEdgeStyle.SolidBoundary
                );
                return root;
            }

            if (style == DungeonEdgeStyle.OpenCrossing)
                return root;

            Transform wallRun = CreateOcclusionSection(root.transform, "Wall Run");
            wallRun.GetComponent<DungeonOcclusionSection>().ConfigureEdge(edge);
            edgeWalls.Clear();
            DungeonRoomGeometry.AppendEdgeWalls(edgeWalls, edge, passage);
            foreach (DungeonWallPiece piece in edgeWalls)
                InstantiateWall(
                    wallRun,
                    piece,
                    DungeonRoomGeometry.IsRunEndPiece(edge, piece, parapetJoinMask)
                );

            BuildArchways(wallRun, edge, passage);
            return root;
        }

        /// <summary>
        /// Builds a safe outer edge in the style the environment's profile asks
        /// for. Masonry-railing themes use the available masonry as a low
        /// railing. The other styles get a renderer-free collision line here;
        /// rock-line themes have their visual dressing supplied by
        /// DungeonPropPlacer, and undressed themes gain theirs once their
        /// profile points at real boundary assets.
        /// </summary>
        private void BuildEnvironmentBoundary(
            Transform parent,
            DungeonEdge edge,
            DungeonLayout.EnvironmentTheme environment,
            bool buildCliffFace
        )
        {
            DungeonEnvironmentProfile profile = DungeonEnvironmentProfile.Of(environment);
            if (profile.BoundaryStyle == DungeonBoundaryStyle.MasonryRailing)
            {
                BuildDungeonRailing(parent, edge, buildCliffFace);
                return;
            }

            GameObject collision = new GameObject($"{environment} Boundary Collision");
            collision.transform.SetParent(parent, false);
            Vector2 center = edge.Horizontal
                ? new Vector2(
                    edge.X * DungeonLayout.RoomWidth,
                    (edge.Y + 0.5f) * DungeonLayout.RoomDepth
                )
                : new Vector2(
                    (edge.X + 0.5f) * DungeonLayout.RoomWidth,
                    edge.Y * DungeonLayout.RoomDepth
                );
            collision.transform.position = center.ToWorld(0.7f);
            var collider = collision.AddComponent<BoxCollider>();
            collider.size = edge.Horizontal
                ? new Vector3(DungeonLayout.RoomWidth, 1.4f, DungeonWallPiece.SlabThickness)
                : new Vector3(DungeonWallPiece.SlabThickness, 1.4f, DungeonLayout.RoomDepth);
            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer >= 0)
                collision.layer = wallLayer;
        }

        /// <summary>
        /// A knee-high dungeon railing. On the camera-facing side, one flat-backed
        /// variant of the same masonry forms the parapet and continues below floor
        /// level as a two-course cliff. All three courses share one face, without
        /// the source wall's apron ledge or a step between the stacked pieces.
        /// </summary>
        private void BuildDungeonRailing(Transform parent, DungeonEdge edge, bool buildCliffFace)
        {
            const float cliffFaceScale = 1.5f;

            Transform parapet = new GameObject("Low Dungeon Railing").transform;
            parapet.SetParent(parent, false);
            parapet.gameObject.AddComponent<DungeonContentRoot>();

            Transform cliffFace = null;
            if (buildCliffFace)
            {
                cliffFace = new GameObject("Cliff Face Below Floor").transform;
                cliffFace.SetParent(parent, false);
                cliffFace.gameObject.AddComponent<DungeonContentRoot>();
            }

            edgeWalls.Clear();
            DungeonRoomGeometry.AppendEdgeWalls(edgeWalls, edge, new DungeonPassage(false, 0, 0));
            foreach (DungeonWallPiece piece in edgeWalls)
            {
                GameObject lip = InstantiateScaledWall(
                    parapet,
                    piece,
                    BoundaryParapetHeightScale,
                    piece.BaseLift,
                    name: buildCliffFace ? "DungeonWall - Cliff Parapet" : null,
                    prefabOverride: buildCliffFace ? boundaryShellPrefab : null
                );
                if (!buildCliffFace)
                    continue;

                if (boundaryShellPrefab == null)
                    DungeonWallBaseTrim.RemoveLooseBase(lip);
                float upperCourseLift =
                    piece.BaseLift - DungeonWallPiece.SlabHeight * cliffFaceScale;
                GameObject upperCourse = InstantiateScaledWall(
                    cliffFace,
                    piece,
                    cliffFaceScale,
                    upperCourseLift,
                    name: "DungeonWall - Cliff Shell",
                    prefabOverride: boundaryShellPrefab
                );
                GameObject lowerCourse = InstantiateScaledWall(
                    cliffFace,
                    piece,
                    cliffFaceScale,
                    upperCourseLift - DungeonWallPiece.SlabHeight * cliffFaceScale,
                    name: "DungeonWall - Cliff Shell",
                    prefabOverride: boundaryShellPrefab
                );
                if (boundaryShellPrefab == null)
                {
                    DungeonWallBaseTrim.RemoveLooseBase(upperCourse);
                    DungeonWallBaseTrim.RemoveLooseBase(lowerCourse);
                }
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

        /// <summary>
        /// Instantiates one planned wall piece on its slab centre line. A piece
        /// meeting the boundary parapet takes the parapet's height, so the two
        /// runs read as one line of masonry turning the corner.
        /// </summary>
        private void InstantiateWall(
            Transform parent,
            DungeonWallPiece piece,
            bool joinsBoundaryParapet
        )
        {
            InstantiateScaledWall(
                parent,
                piece,
                joinsBoundaryParapet ? BoundaryParapetHeightScale : SharedEdgeRailingHeightScale,
                piece.BaseLift,
                name: joinsBoundaryParapet
                    ? "DungeonWall - Shared Railing At Boundary"
                    : "DungeonWall - Shared Half-Height Railing"
            );
        }

        /// <summary>Instantiates one wall piece, and hands it back so a caller
        /// can dress or trim it.</summary>
        private GameObject InstantiateScaledWall(
            Transform parent,
            DungeonWallPiece piece,
            float verticalScale,
            float lift,
            string name = null,
            GameObject prefabOverride = null
        )
        {
            GameObject wall = Instantiate(
                prefabOverride != null ? prefabOverride : wallPrefab,
                piece.PrefabPosition.ToWorld(lift),
                piece.AlongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f),
                parent
            );
            wall.name =
                name ?? (verticalScale < 1f ? "DungeonWall - Low Parapet" : "DungeonWall - Cliff");
            wall.transform.localScale = Vector3.Scale(
                wall.transform.localScale,
                new Vector3(1f, verticalScale, 1f)
            );
            return wall;
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
