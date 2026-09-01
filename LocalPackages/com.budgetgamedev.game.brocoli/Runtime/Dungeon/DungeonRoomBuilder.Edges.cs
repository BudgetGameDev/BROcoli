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
        /// </summary>
        public GameObject BuildEdge(
            Transform parent,
            DungeonEdge edge,
            DungeonPassage passage,
            DungeonEdgeStyle style = DungeonEdgeStyle.Interior,
            DungeonLayout.EnvironmentTheme environment = DungeonLayout.EnvironmentTheme.Dungeon
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
                InstantiateWall(wallRun, piece);

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
        /// A knee-high dungeon railing. On the camera-facing side, the same
        /// modular masonry continues below floor level as a two-course cliff.
        /// </summary>
        private void BuildDungeonRailing(Transform parent, DungeonEdge edge, bool buildCliffFace)
        {
            const float parapetScale = 0.3f;
            const float cliffFaceScale = 1.5f;
            const float lowerCourseOutset = 0.45f;

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
                InstantiateScaledWall(parapet, piece, parapetScale, piece.BaseLift);
                if (!buildCliffFace)
                    continue;
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
            InstantiateScaledWall(
                parent,
                piece,
                SharedEdgeRailingHeightScale,
                piece.BaseLift,
                name: "DungeonWall - Shared Half-Height Railing"
            );
        }

        private void InstantiateScaledWall(
            Transform parent,
            DungeonWallPiece piece,
            float verticalScale,
            float lift,
            float outwardOffset = 0f,
            string name = null
        )
        {
            // Cliff courses step outward away from the playable floor: south
            // for the camera-facing cliff, west for the side cliffs the yawed
            // camera sees at the platform's stair-steps.
            GameObject wall = Instantiate(
                wallPrefab,
                (piece.PrefabPosition - piece.Normal * outwardOffset).ToWorld(lift),
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
