using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>A wall-mounted fitting: where it hangs and which way it faces.</summary>
    public readonly struct DungeonWallMount
    {
        /// <summary>Room-local ground-plane position.</summary>
        public readonly Vector2 Local;
        public readonly float Yaw;

        public DungeonWallMount(Vector2 local, float yaw)
        {
            Local = local;
            Yaw = yaw;
        }
    }

    /// <summary>
    /// Chooses where torches hang. Kept apart from the prop placer so the one rule
    /// that matters - nothing is ever mounted in a doorway - can be checked
    /// arithmetically instead of by looking at the room.
    /// </summary>
    public static partial class DungeonWallDressing
    {
        private const float HalfRoomWidth = DungeonLayout.RoomWidth / 2f;
        private const float HalfRoomDepth = DungeonLayout.RoomDepth / 2f;

        // Half width of a torch bracket plus a little breathing room. Anything
        // closer than this to an opening's centre line reads as hanging in the
        // doorway rather than being mounted on the wall beside it.
        public const float TorchDoorwayClearance = 0.9f;

        private const float TorchSpacing = 3f;

        /// <summary>
        /// The room's torch mounting points, minus any that would sit inside a
        /// doorway. When the shape's preferred spots are mostly doorways, the
        /// remaining solid outer-wall pieces make up the difference so a room is
        /// never left unlit.
        /// </summary>
        public static List<DungeonWallMount> TorchMounts(
            DungeonLayout.RoomArchetype archetype,
            DungeonLayout.RoomDoorways doorways,
            int wanted,
            System.Random random
        )
        {
            var mounts = new List<DungeonWallMount>();
            foreach (DungeonWallMount mount in ShapeTorchMounts(archetype))
            {
                if (!doorways.BlocksDoorway(mount.Local, TorchDoorwayClearance))
                    mounts.Add(mount);
            }
            Shuffle(mounts, random);
            if (mounts.Count >= wanted)
                return mounts;

            List<DungeonWallMount> fallback = SolidOuterWallTorchMounts(doorways);
            Shuffle(fallback, random);
            foreach (DungeonWallMount mount in fallback)
            {
                if (mounts.Count >= wanted)
                    break;
                if (!IsClearOfMounts(mounts, mount.Local))
                    continue;
                mounts.Add(mount);
            }
            return mounts;
        }

        /// <summary>
        /// One mounting point per outer-wall piece that survived the doorway cuts.
        /// </summary>
        private static List<DungeonWallMount> SolidOuterWallTorchMounts(
            DungeonLayout.RoomDoorways doorways
        )
        {
            var mounts = new List<DungeonWallMount>();
            for (int slot = 1; slot < DungeonLayout.RoomTilesX - 1; slot++)
            {
                float x = DungeonPassage.SlotOffset(slot, DungeonLayout.RoomTilesX);
                if (!doorways.North.HasOpening(slot))
                    mounts.Add(
                        new DungeonWallMount(new Vector2(x, InnerFace(HalfRoomDepth)), 180f)
                    );
            }

            for (int slot = 1; slot < DungeonLayout.RoomTilesZ - 1; slot++)
            {
                float z = DungeonPassage.SlotOffset(slot, DungeonLayout.RoomTilesZ);
                if (!doorways.East.HasOpening(slot))
                    mounts.Add(
                        new DungeonWallMount(new Vector2(InnerFace(HalfRoomWidth), z), -90f)
                    );
                if (!doorways.West.HasOpening(slot))
                    mounts.Add(
                        new DungeonWallMount(new Vector2(InnerFace(-HalfRoomWidth), z), 90f)
                    );
            }
            return mounts;
        }

        private static bool IsClearOfMounts(List<DungeonWallMount> mounts, Vector2 candidate)
        {
            foreach (DungeonWallMount mount in mounts)
            {
                if ((mount.Local - candidate).sqrMagnitude < TorchSpacing * TorchSpacing)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// The face of the wall at <paramref name="wallCoordinate"/> that looks back
        /// towards the room centre. The coordinate is signed relative to that
        /// centre, so this is symmetric on all four sides.
        /// </summary>
        public static float InnerFace(float wallCoordinate)
        {
            return wallCoordinate - Mathf.Sign(wallCoordinate) * DungeonWallPiece.SlabHalfThickness;
        }

        /// <summary>The opposite face, looking away from the room centre.</summary>
        public static float OuterFace(float wallCoordinate)
        {
            return wallCoordinate + Mathf.Sign(wallCoordinate) * DungeonWallPiece.SlabHalfThickness;
        }

        private static void Shuffle<T>(List<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
    }
}
