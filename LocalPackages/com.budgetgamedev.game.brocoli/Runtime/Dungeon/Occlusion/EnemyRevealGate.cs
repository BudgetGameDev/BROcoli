using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Decides whether an enemy is allowed to pull a wall down. Lowering a wall is
    /// visible from anywhere on screen, so an enemy the player has not reached yet
    /// would announce itself - and the contents of a room - before the room is
    /// entered. An enemy only earns a lowered wall once the player shares its
    /// logical room, including another cell of the same merged mega room.
    /// </summary>
    public static class EnemyRevealGate
    {
        public static bool IsRevealed(Vector3 playerPosition, Vector3 enemyPosition)
        {
            return IsRevealed(playerPosition, enemyPosition, null);
        }

        public static bool IsRevealed(
            Vector3 playerPosition,
            Vector3 enemyPosition,
            DungeonLayout layout
        )
        {
            Vector2 player = new(playerPosition.x, playerPosition.z);
            Vector2 enemy = new(enemyPosition.x, enemyPosition.z);
            Vector2Int playerRoom = DungeonLayout.RoomAt(player);
            Vector2Int enemyRoom = DungeonLayout.RoomAt(enemy);
            return layout != null
                ? layout.AreInSameRoom(playerRoom, enemyRoom)
                : playerRoom == enemyRoom;
        }
    }
}
