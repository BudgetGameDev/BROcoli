using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Describes the openings cut into one shared wall run. An opening is a
    /// missing wall piece, optionally framed by an archway prefab. A closed edge
    /// has no openings at all and reads as an unbroken wall.
    /// </summary>
    public readonly struct DungeonPassage
    {
        public readonly bool Open;
        public readonly int OpeningMask;
        public readonly int ArchwayMask;

        public DungeonPassage(bool open, int openingMask, int archwayMask)
        {
            Open = open;
            OpeningMask = openingMask;
            ArchwayMask = archwayMask;
        }

        public bool HasOpening(int slot)
        {
            return (OpeningMask & (1 << slot)) != 0;
        }

        public bool HasArchway(int slot)
        {
            return (ArchwayMask & (1 << slot)) != 0;
        }

        /// <summary>The wall-run offset of a slot, measured from the run's centre.</summary>
        public static float SlotOffset(int slot, int slotCount)
        {
            return (slot - slotCount / 2) * DungeonLayout.TileSize;
        }

        /// <summary>
        /// Whether an object mounted <paramref name="offset"/> along this wall run
        /// would stand in one of its openings. <paramref name="clearance"/> is the
        /// object's own half width.
        /// </summary>
        public bool OverlapsOpening(float offset, int slotCount, float clearance)
        {
            float reach = DungeonLayout.TileSize * 0.5f + clearance;
            for (int slot = 0; slot < slotCount; slot++)
            {
                if (!HasOpening(slot))
                    continue;
                if (Mathf.Abs(offset - SlotOffset(slot, slotCount)) < reach)
                    return true;
            }
            return false;
        }

        public int OpeningCount
        {
            get
            {
                int count = 0;
                int remaining = OpeningMask;
                while (remaining != 0)
                {
                    count += remaining & 1;
                    remaining >>= 1;
                }
                return count;
            }
        }
    }
}
