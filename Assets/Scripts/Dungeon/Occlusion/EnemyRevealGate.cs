using UnityEngine;

/// <summary>
/// Decides whether an enemy is allowed to pull a wall down. Lowering a wall is
/// visible from anywhere on screen, so an enemy the player has not reached yet
/// would announce itself - and the contents of a room - before the room is
/// entered. An enemy only earns a lowered wall once the player shares its room
/// or has walked up to it.
/// </summary>
public static class EnemyRevealGate
{
    /// <summary>How close the player must come to an enemy in another room.</summary>
    public const float DefaultApproachRadius = 6f;

    public static bool IsRevealed(Vector3 playerPosition, Vector3 enemyPosition)
    {
        return IsRevealed(playerPosition, enemyPosition, DefaultApproachRadius);
    }

    public static bool IsRevealed(
        Vector3 playerPosition,
        Vector3 enemyPosition,
        float approachRadius
    )
    {
        Vector2 player = new(playerPosition.x, playerPosition.z);
        Vector2 enemy = new(enemyPosition.x, enemyPosition.z);
        if (DungeonLayout.RoomAt(player) == DungeonLayout.RoomAt(enemy))
            return true;

        // The doorway case: the player is close enough that the enemy is about
        // to become their problem, so the wall between them may still lower.
        return (player - enemy).sqrMagnitude <= approachRadius * approachRadius;
    }
}
