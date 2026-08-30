using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    internal static partial class WallVisibilityInvariants
    {
        private static bool WasInTheGap(
            WallVisibilitySimulation.Result result,
            int index,
            int window,
            int pieceId
        )
        {
            for (int i = Mathf.Max(0, index - window); i <= index; i++)
            {
                if (result.Frames[i].GapPieces.Contains(pieceId))
                    return true;
            }
            return false;
        }

        private static bool WasAskedFor(
            WallVisibilitySimulation.Result result,
            int index,
            int window,
            int groupId
        )
        {
            for (int i = Mathf.Max(0, index - window); i <= index; i++)
            {
                WallVisibilitySimulation.Frame frame = result.Frames[i];
                if (frame.Activated.Contains(groupId))
                    return true;
            }
            return false;
        }

        /// <summary>How deep the nearest solid corner of a piece stands.</summary>
        private static float FrontDepth(
            WallVisibilitySimulation.Result result,
            WallVisibilitySimulation.Frame frame,
            int pieceId
        )
        {
            return Depth(result, frame, pieceId, nearest: true);
        }

        /// <summary>How deep the rear-most solid corner of a piece stands.</summary>
        private static float RearDepth(
            WallVisibilitySimulation.Result result,
            WallVisibilitySimulation.Frame frame,
            int pieceId
        )
        {
            return Depth(result, frame, pieceId, nearest: false);
        }

        private static float Depth(
            WallVisibilitySimulation.Result result,
            WallVisibilitySimulation.Frame frame,
            int pieceId,
            bool nearest
        )
        {
            Bounds bounds = result.World.PieceOf(pieceId).Structure;
            float depth = nearest ? float.PositiveInfinity : float.NegativeInfinity;
            for (int x = 0; x <= 1; x++)
            for (int z = 0; z <= 1; z++)
            {
                var corner = new Vector3(
                    x == 0 ? bounds.min.x : bounds.max.x,
                    0f,
                    z == 0 ? bounds.min.z : bounds.max.z
                );
                float cornerDepth = Vector3.Dot(corner - frame.CameraPosition, frame.GroundForward);
                depth = nearest ? Mathf.Min(depth, cornerDepth) : Mathf.Max(depth, cornerDepth);
            }
            return depth;
        }
    }
}
