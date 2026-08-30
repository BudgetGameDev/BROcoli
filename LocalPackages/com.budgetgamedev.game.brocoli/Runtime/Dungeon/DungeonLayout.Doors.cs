using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class DungeonLayout
    {
        private bool IsEdgeBaseOpen(DungeonEdge edge)
        {
            uint hash = Hash(edge.X, edge.Y, edge.Horizontal ? EdgeSalt : EdgeSalt + 1);
            return hash / (float)uint.MaxValue < DoorOpenChance;
        }

        /// <summary>
        /// The doors this room forces open on top of the base rolls, one bit per
        /// direction. A room whose edges all rolled closed forces two exits (just
        /// one when it is a mega-room cell, which already reaches more exits
        /// through its cluster), and a room left with a single door usually forces
        /// a second, so dead ends stay rare instead of common. Cluster-internal
        /// edges are already open and are never counted or forced.
        /// </summary>
        private int ForcedDoorMask(Vector2Int room)
        {
            int closedMask = 0;
            int openCount = 0;
            for (int direction = 0; direction < 4; direction++)
            {
                DungeonEdge edge = EdgeBetween(room, direction);
                if (IsClusterInternalEdge(edge))
                    continue;
                if (IsEdgeBaseOpen(edge))
                    openCount++;
                else
                    closedMask |= 1 << direction;
            }

            if (openCount >= 2 || closedMask == 0)
                return 0;

            if (openCount == 1)
            {
                bool breakDeadEnd =
                    Hash(room.x, room.y, DeadEndBreakSalt) / (float)uint.MaxValue
                    < SecondDoorChance;
                return breakDeadEnd ? PickDirectionBit(room, SecondDoorSalt, closedMask) : 0;
            }

            int forced = PickDirectionBit(room, ForcedDoorSalt, closedMask);
            if (!IsMegaRoomCell(room))
                forced |= PickDirectionBit(room, SecondDoorSalt, closedMask & ~forced);
            return forced;
        }

        /// <summary>One deterministic direction bit out of a candidate mask.</summary>
        private int PickDirectionBit(Vector2Int room, int salt, int candidateMask)
        {
            int count = 0;
            for (int direction = 0; direction < 4; direction++)
            {
                if ((candidateMask & (1 << direction)) != 0)
                    count++;
            }
            if (count == 0)
                return 0;

            int chosen = (int)(Hash(room.x, room.y, salt) % (uint)count);
            for (int direction = 0; direction < 4; direction++)
            {
                if ((candidateMask & (1 << direction)) == 0)
                    continue;
                if (chosen-- == 0)
                    return 1 << direction;
            }
            return 0;
        }

        private uint Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)x * 0x9E3779B1u;
                h = (h << 13) | (h >> 19);
                h *= 0x85EBCA6Bu;
                h ^= (uint)y * 0xC2B2AE35u;
                h = (h << 11) | (h >> 21);
                h *= 0x27D4EB2Fu;
                h ^= (uint)salt * 0x165667B1u;
                h ^= h >> 15;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                return h;
            }
        }
    }
}
