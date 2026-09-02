using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonRoomBuilder
    {
        private readonly List<DungeonWallPiece> edgeWalls = new();
        private readonly List<DungeonArchway> edgeArchways = new();
        private readonly List<DungeonBoundaryCourse> boundaryCourses = new();

        /// <summary>
        /// Builds one shared half-height railing run between two rooms from its
        /// planned geometry. An open run drops one to three pieces to form
        /// doorways, at most one of which is framed by an archway; a closed run is
        /// unbroken. Both neighbouring rooms share this geometry.
        /// <paramref name="parapetJoinMask"/> names the ends of the run that land
        /// on a corner where the platform's boundary parapet turns, so those
        /// pieces can be built to the parapet's height instead.
        ///
        /// An outer boundary is built from the structural masonry in every
        /// <paramref name="environment"/>; the theme decides only what
        /// <see cref="DungeonPropPlacer.BuildBoundaryDressing"/> stands on top of
        /// it, and is carried here for the boundary kit a theme may yet get.
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

            if (DungeonRoomGeometry.IsPlatformBoundary(style))
            {
                BuildBoundaryFacade(root.transform, edge);
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
        /// Builds the platform's outer edge: a knee-high parapet at floor level
        /// standing on the cliff courses that carry it down past the floor, one
        /// stack per slot of the run.
        ///
        /// Every boundary is built this way, whatever environment it stands in and
        /// whichever way it faces. A theme without its own boundary kit used to get
        /// an invisible collision line and a boundary facing away from the camera
        /// used to get a parapet with nothing under it; both left stretches of the
        /// platform ending in raw void, which is the one thing an outer edge exists
        /// to stop. Themes still dress this masonry with their own rock lines
        /// through <see cref="DungeonPropPlacer.BuildBoundaryDressing"/>, so a cave
        /// reads as a cave -- standing on the same structural cliff as everything
        /// else rather than in place of one.
        /// </summary>
        private void BuildBoundaryFacade(Transform parent, DungeonEdge edge)
        {
            Transform parapet = BoundaryRoot(parent, "Low Dungeon Railing");
            Transform cliffFace = BoundaryRoot(parent, "Cliff Face Below Floor");

            edgeWalls.Clear();
            DungeonRoomGeometry.AppendEdgeWalls(edgeWalls, edge, new DungeonPassage(false, 0, 0));
            boundaryCourses.Clear();
            foreach (DungeonWallPiece piece in edgeWalls)
                DungeonRoomGeometry.AppendBoundaryCourses(boundaryCourses, piece);

            foreach (DungeonBoundaryCourse course in boundaryCourses)
            {
                GameObject built = InstantiateScaledWall(
                    course.Parapet ? parapet : cliffFace,
                    course.Piece,
                    course.HeightScale,
                    course.Lift,
                    name: course.Parapet
                        ? "DungeonWall - Cliff Parapet"
                        : "DungeonWall - Cliff Shell",
                    prefabOverride: boundaryShellPrefab
                );

                // The flat-backed shell mesh has no apron to trim. Without it the
                // ordinary wall stands in, and its base ledge would step out of the
                // cliff face at every seam.
                if (boundaryShellPrefab == null)
                    DungeonWallBaseTrim.RemoveLooseBase(built);
            }
        }

        private static Transform BoundaryRoot(Transform parent, string name)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.gameObject.AddComponent<DungeonContentRoot>();
            return root;
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
