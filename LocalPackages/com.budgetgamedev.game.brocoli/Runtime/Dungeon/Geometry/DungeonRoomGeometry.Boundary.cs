using System.Collections.Generic;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// One stacked course of the platform's outer boundary facade: a wall piece,
    /// how tall it is built, and where its base is seated relative to the floor
    /// it stands beside.
    /// </summary>
    public readonly struct DungeonBoundaryCourse
    {
        public readonly DungeonWallPiece Piece;

        /// <summary>Vertical scale applied to the modular wall mesh.</summary>
        public readonly float HeightScale;

        /// <summary>Where this course's base sits; negative is below the floor.</summary>
        public readonly float Lift;

        /// <summary>True for the knee-high lip standing at floor level.</summary>
        public readonly bool Parapet;

        public DungeonBoundaryCourse(
            DungeonWallPiece piece,
            float heightScale,
            float lift,
            bool parapet
        )
        {
            Piece = piece;
            HeightScale = heightScale;
            Lift = lift;
            Parapet = parapet;
        }

        /// <summary>How far above its base this course's masonry reaches.</summary>
        public float Height => DungeonWallPiece.SlabHeight * HeightScale;

        /// <summary>The height this course's top surface lands on.</summary>
        public float Top => Lift + Height;
    }

    public static partial class DungeonRoomGeometry
    {
        /// <summary>
        /// Courses of the cliff face hanging under a boundary's parapet. Two of
        /// them, plus the parapet itself, are the three stacked rows that make an
        /// outer edge read as the side of a raised platform. Anything less and the
        /// boundary is a railing with raw void beneath it, which is what the
        /// yawed gameplay camera looks straight at.
        /// </summary>
        public const int CliffCourses = 2;

        /// <summary>Stacked rows of a boundary facade, the parapet included.</summary>
        public const int BoundaryCourses = CliffCourses + 1;

        /// <summary>
        /// Vertical scale of one cliff course. Taller than the source mesh so two
        /// of them clear the gameplay camera's view under the platform without
        /// stacking a visible seam every couple of metres.
        /// </summary>
        public const float CliffCourseHeightScale = 1.5f;

        /// <summary>
        /// The stack standing on one boundary slot: the parapet at floor level and
        /// the cliff courses hanging below it, seated so that each course's top
        /// meets the base of the one above and all of them share a single face.
        /// </summary>
        public static void AppendBoundaryCourses(
            List<DungeonBoundaryCourse> courses,
            DungeonWallPiece piece
        )
        {
            courses.Add(
                new DungeonBoundaryCourse(
                    piece,
                    DungeonRoomBuilder.BoundaryParapetHeightScale,
                    piece.BaseLift,
                    true
                )
            );

            float lift = piece.BaseLift;
            for (int course = 0; course < CliffCourses; course++)
            {
                lift -= DungeonWallPiece.SlabHeight * CliffCourseHeightScale;
                courses.Add(new DungeonBoundaryCourse(piece, CliffCourseHeightScale, lift, false));
            }
        }

        /// <summary>
        /// Whether an edge style is an outer boundary of the playable platform, and
        /// so carries the facade rather than a shared run or an open crossing. The
        /// three boundary styles differ in what the camera sees past them, never in
        /// whether the platform ends there.
        /// </summary>
        public static bool IsPlatformBoundary(DungeonEdgeStyle style) =>
            style
                is DungeonEdgeStyle.SolidBoundary
                    or DungeonEdgeStyle.SouthCliff
                    or DungeonEdgeStyle.SideCliff;
    }
}
