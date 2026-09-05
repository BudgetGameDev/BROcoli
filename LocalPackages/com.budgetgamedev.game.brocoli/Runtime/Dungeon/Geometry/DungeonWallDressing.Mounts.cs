using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public static partial class DungeonWallDressing
    {
        /// <summary>
        /// Mount on the masonry that the room actually builds. Broad platform passages can
        /// remove every shell candidate; curved and diagonal rooms still have interior railings.
        /// Using their geometry keeps lighting in step with new shapes and wall height changes.
        /// </summary>
        private static List<DungeonWallMount> ShapeTorchMounts(
            DungeonLayout.RoomArchetype archetype
        )
        {
            var mounts = new List<DungeonWallMount>();
            var walls = new List<DungeonWallPiece>();
            DungeonRoomGeometry.AppendInteriorWalls(walls, Vector2Int.zero, archetype);
            foreach (DungeonWallPiece wall in walls)
            {
                float scale =
                    wall.Kind == DungeonWallKind.InteriorFeature ? 1f
                    : wall.AlongX ? DungeonRoomBuilder.InteriorRailingHeightScale
                    : DungeonRoomBuilder.InteriorWallHeightScale;
                mounts.Add(OnMasonry(wall.Anchor, wall.Normal, scale));
            }

            var railings = new List<DungeonRailingSegment>();
            DungeonRoomGeometry.AppendInteriorRailings(railings, Vector2Int.zero, archetype);
            foreach (DungeonRailingSegment railing in railings)
                mounts.Add(
                    OnMasonry(
                        railing.Center,
                        railing.Normal,
                        DungeonRoomBuilder.InteriorRailingHeightScale,
                        railing.BaseLift
                    )
                );
            return mounts;
        }

        private static DungeonWallMount OnMasonry(
            Vector2 center,
            Vector2 normal,
            float heightScale,
            float lift = 0f
        )
        {
            // Face into the room where possible, keeping the bracket on the slab face.
            if (Vector2.Dot(normal, -center) < 0f)
                normal = -normal;
            Vector2 point = center + normal * DungeonWallPiece.SlabHalfThickness;
            float yaw = Mathf.Atan2(normal.x, normal.y) * Mathf.Rad2Deg;
            float height = DungeonWallPiece.SlabHeight * (heightScale - 1f) + lift;
            return new DungeonWallMount(point, yaw, height);
        }
    }
}
