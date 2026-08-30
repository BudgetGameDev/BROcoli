using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonManager
    {
        internal BrocoliDungeonSave CaptureRunState()
        {
            var save = new BrocoliDungeonSave { seed = seed, roomsVisited = roomsVisited };
            var coordinates = new List<Vector2Int>(roomStates.Keys);
            coordinates.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            foreach (Vector2Int coordinate in coordinates)
            {
                RoomState state = roomStates[coordinate];
                if (!state.Visited && state.OpenedChestSlots.Count == 0)
                    continue;

                var room = new BrocoliRoomSave
                {
                    x = coordinate.x,
                    y = coordinate.y,
                    visited = state.Visited,
                    openedChestSlots = new List<int>(state.OpenedChestSlots),
                };
                room.openedChestSlots.Sort();
                save.rooms.Add(room);
            }

            return save;
        }

        private void RestoreRunState(BrocoliDungeonSave save)
        {
            if (save == null)
                return;

            seed = save.seed;
            roomsVisited = Mathf.Max(0, save.roomsVisited);
            roomStates.Clear();
            if (save.rooms == null)
                return;

            foreach (BrocoliRoomSave savedRoom in save.rooms)
            {
                if (savedRoom == null)
                    continue;

                var state = new RoomState { Visited = savedRoom.visited };
                if (savedRoom.openedChestSlots != null)
                {
                    foreach (int slot in savedRoom.openedChestSlots)
                    {
                        if (slot >= 0)
                            state.OpenedChestSlots.Add(slot);
                    }
                }
                roomStates[new Vector2Int(savedRoom.x, savedRoom.y)] = state;
            }
        }
    }
}
