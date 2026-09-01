using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// One short, freely rotated railing piece: the primitive that lets the
    /// dungeon curve. Where <see cref="DungeonWallPiece"/> is a full-length slab
    /// locked to the two grid axes (and everything the occlusion Rect maths
    /// reasons about), a railing segment is a knee-high length of the same
    /// masonry between two arbitrary ground points. Chains of them approximate
    /// arcs, serpentine paths, and diagonal lanes. They are always realized
    /// below <see cref="DungeonOccluder.MinimumAutomaticFadeHeight"/>, so they
    /// never participate in camera occlusion and never need an axis-aligned
    /// footprint - only prop placement has to keep clear of them, and that is a
    /// point-to-segment distance question.
    /// </summary>
    public readonly struct DungeonRailingSegment
    {
        /// <summary>
        /// Segments seat between the two axis-aligned lift planes (0.002 and
        /// 0.004, see <see cref="DungeonWallPiece"/>) and step a hair per piece,
        /// so no two overlapping base aprons in a chain - or an apron crossing
        /// an axis-aligned run - ever share a plane to z-fight on.
        /// </summary>
        public const float BaseLiftFloor = 0.0024f;
        public const float LiftStepSize = 0.0002f;
        public const int LiftStepCount = 5;

        /// <summary>Half the structural slab thickness, same masonry as walls.</summary>
        public const float SlabHalfThickness = DungeonWallPiece.SlabHalfThickness;

        public readonly Vector2 Start;
        public readonly Vector2 End;

        /// <summary>Which of the staggered lift planes this piece seats on.</summary>
        public readonly int LiftStep;

        public DungeonRailingSegment(Vector2 start, Vector2 end, int liftStep)
        {
            Start = start;
            End = end;
            LiftStep = ((liftStep % LiftStepCount) + LiftStepCount) % LiftStepCount;
        }

        public Vector2 Center => (Start + End) * 0.5f;

        public float Length => Vector2.Distance(Start, End);

        /// <summary>Unit direction the railing runs in.</summary>
        public Vector2 Direction => (End - Start).normalized;

        /// <summary>
        /// The slab's thickness direction: the run direction turned a quarter
        /// left, matching <see cref="DungeonWallPiece.Normal"/> for an east-west
        /// piece.
        /// </summary>
        public Vector2 Normal
        {
            get
            {
                Vector2 direction = Direction;
                return new Vector2(-direction.y, direction.x);
            }
        }

        /// <summary>
        /// Yaw for the wall prefab so its local X axis lands on
        /// <see cref="Direction"/>. Unity's positive yaw turns ground vectors
        /// clockwise, hence the negated angle.
        /// </summary>
        public float YawDegrees
        {
            get
            {
                Vector2 direction = Direction;
                return -Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            }
        }

        /// <summary>
        /// Where the prefab root goes so the slab's centre line lands on the
        /// segment, mirroring <see cref="DungeonWallPiece.PrefabPosition"/>.
        /// </summary>
        public Vector2 PrefabPosition => Center - Normal * DungeonWallPiece.SlabCenterOffset;

        /// <summary>The prefab's X scale that trims its slab to this length.</summary>
        public float LengthScale => Length / DungeonWallPiece.NominalLength;

        public float BaseLift => BaseLiftFloor + LiftStep * LiftStepSize;

        /// <summary>
        /// Ground distance from a point to this segment's slab centre line.
        /// Prop clearance is this minus <see cref="SlabHalfThickness"/>.
        /// </summary>
        public float DistanceTo(Vector2 point)
        {
            Vector2 run = End - Start;
            float lengthSquared = run.sqrMagnitude;
            if (lengthSquared < 1e-6f)
                return Vector2.Distance(point, Start);
            float along = Mathf.Clamp01(Vector2.Dot(point - Start, run) / lengthSquared);
            return Vector2.Distance(point, Start + run * along);
        }

        public override string ToString()
        {
            return $"Railing {Start} -> {End}";
        }
    }
}
