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
        public readonly float HeightOffset;

        public DungeonWallMount(Vector2 local, float yaw, float heightOffset = 0f)
        {
            Local = local;
            Yaw = yaw;
            HeightOffset = heightOffset;
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

        /// <summary>
        /// How far a wall fitting drops below its shell-wall hanging height when
        /// its mount is an interior run. The torch prefab is proportioned for
        /// the full-height shell slab, with its flame near that slab's top edge;
        /// hung unchanged on a half-height interior wall the whole fitting
        /// floats above the masonry. Dropping it this far seats the flame just
        /// over the interior wall's top edge while keeping the bracket clear of
        /// the floor.
        /// </summary>
        public const float InteriorMountDrop = 0.8f;

        /// <summary>Every shell side present, as bits of (1 &lt;&lt; direction).</summary>
        public const int AllShellWalls = 0b1111;

        private const float TorchSpacing = 3f;

        /// <summary>
        /// The room's torch mounting points, minus any that would sit inside a
        /// doorway or hang on a shell side missing from
        /// <paramref name="shellWallMask"/>. The platform boundary replaces some
        /// shell walls with railings, rock lines, or open air, and a fitting
        /// mounted there would float in mid-air at the level edge. When the
        /// shape's preferred spots are mostly doorways, the remaining solid
        /// outer-wall pieces make up the difference so a room is never left
        /// unlit.
        /// </summary>
        public static List<DungeonWallMount> TorchMounts(
            DungeonLayout.RoomArchetype archetype,
            DungeonLayout.RoomDoorways doorways,
            int wanted,
            System.Random random,
            int shellWallMask = AllShellWalls
        )
        {
            var mounts = new List<DungeonWallMount>();
            var preferred = ShapeTorchMounts(archetype);
            Shuffle(preferred, random);
            foreach (DungeonWallMount mount in preferred)
            {
                if (!HasShellWall(mount.Local, shellWallMask))
                    continue;
                if (
                    !doorways.BlocksDoorway(mount.Local, TorchDoorwayClearance)
                    && IsClearOfMounts(mounts, mount.Local)
                )
                    mounts.Add(mount);
            }
            if (mounts.Count >= wanted)
                return mounts;

            List<DungeonWallMount> fallback = SolidOuterWallTorchMounts(doorways);
            Shuffle(fallback, random);
            foreach (DungeonWallMount mount in fallback)
            {
                if (mounts.Count >= wanted)
                    break;
                if (!HasShellWall(mount.Local, shellWallMask))
                    continue;
                if (!IsClearOfMounts(mounts, mount.Local))
                    continue;
                mounts.Add(mount);
            }
            return mounts;
        }

        /// <summary>
        /// Whether the wall a mount hangs on actually exists. A mount away from
        /// the outer shell hangs on an interior run, which the platform never
        /// removes; a mount on a shell face needs that side's bit in the mask.
        /// </summary>
        public static bool HasShellWall(Vector2 local, int shellWallMask)
        {
            int side = RequiredShellWall(local);
            return side < 0 || (shellWallMask & (1 << side)) != 0;
        }

        /// <summary>
        /// The shell side a mounting point hangs on, or -1 for an interior wall
        /// run.
        /// </summary>
        public static int RequiredShellWall(Vector2 local)
        {
            const float epsilon = 0.05f;
            if (Mathf.Abs(local.y - InnerFace(HalfRoomDepth)) < epsilon)
                return DungeonLayout.North;
            if (Mathf.Abs(local.y - InnerFace(-HalfRoomDepth)) < epsilon)
                return DungeonLayout.South;
            if (Mathf.Abs(local.x - InnerFace(HalfRoomWidth)) < epsilon)
                return DungeonLayout.East;
            if (Mathf.Abs(local.x - InnerFace(-HalfRoomWidth)) < epsilon)
                return DungeonLayout.West;
            return -1;
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
