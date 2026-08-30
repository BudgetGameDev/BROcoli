using UnityEngine;

/// <summary>
/// Marks a transform that holds a room's contents rather than being part of
/// one object. It carries no behaviour; it exists so
/// <see cref="DungeonOccluder"/> can tell where one prop ends and the room
/// around it begins without being told what any of the props are.
///
/// Without this the search for a prop's root would climb until it ran out of
/// parents and read a whole room as a single object, so everything in it would
/// fade at once the moment a barrel stood in front of the player.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonContentRoot : MonoBehaviour { }
