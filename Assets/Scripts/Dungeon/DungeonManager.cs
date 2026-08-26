using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the dungeon-crawler game mode: an infinite grid of procedurally
/// generated rooms joined by doorways. Rooms (geometry, props, loot, and a
/// dormant enemy group) are generated one step ahead of the player, so
/// whatever waits behind a doorway already exists before it is entered.
/// Far-away rooms unload; the deterministic seed rebuilds them identically
/// when the player backtracks.
/// </summary>
public class DungeonManager : MonoBehaviour
{
    private const string EnemyResourceFolder = "CursedDevolpmentStudioAss Assets/Waves";
    private const float RoomCheckInterval = 0.2f;

    [SerializeField]
    private DungeonRoomBuilder builder;

    [SerializeField]
    [Tooltip("0 picks a fresh random seed every run.")]
    private int seed;

    [SerializeField, Min(1)]
    private int unloadDistance = 2;

    private sealed class RoomState
    {
        public bool Visited;
        public bool ChestOpened;
    }

    private sealed class LoadedRoom
    {
        public GameObject Root;
        public List<EnemyBase> DormantEnemies;
    }

    private readonly Dictionary<Vector2Int, LoadedRoom> loadedRooms = new();
    private readonly Dictionary<DungeonEdge, GameObject> loadedEdges = new();
    private readonly Dictionary<Vector2Int, GameObject> loadedCorners = new();
    private readonly Dictionary<Vector2Int, RoomState> roomStates = new();
    private readonly List<EnemyBase> enemyPrefabs = new();

    private DungeonLayout layout;
    private Transform player;
    private Vector2Int currentRoom;
    private bool hasCurrentRoom;
    private float nextRoomCheck;

    public int Seed => seed;

    private void Start()
    {
        if (seed == 0)
            seed = Random.Range(1, int.MaxValue);
        layout = new DungeonLayout(seed);
        Debug.Log($"DungeonManager: generating dungeon with seed {seed}.");

        LoadEnemyPrefabs();
        EnterRoom(Vector2Int.zero);
    }

    private void Update()
    {
        if (Time.time < nextRoomCheck)
            return;
        nextRoomCheck = Time.time + RoomCheckInterval;

        if (player == null)
        {
            player = ResolvePlayer();
            if (player == null)
                return;
        }

        Vector2Int room = DungeonLayout.RoomAt(player.position.ToGround());
        if (!hasCurrentRoom || room != currentRoom)
            EnterRoom(room);
    }

    private void EnterRoom(Vector2Int room)
    {
        currentRoom = room;
        hasCurrentRoom = true;

        EnsureRoom(room);
        DungeonEnemyPlacer.Activate(loadedRooms[room].DormantEnemies);
        GetState(room).Visited = true;

        // Pre-generate the full 3x3 neighbourhood so anything visible over a
        // wall or reachable through a doorway already exists before the
        // player gets there.
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
            EnsureRoom(room + new Vector2Int(dx, dy));

        UnloadDistantRooms();
    }

    private void EnsureRoom(Vector2Int room)
    {
        if (loadedRooms.ContainsKey(room))
            return;

        RoomState state = GetState(room);
        var root = new GameObject($"Room ({room.x}, {room.y})");
        root.transform.SetParent(transform, false);

        builder.BuildFloor(root.transform, room, layout.RoomRandom(room, 404));
        LootChest chest = builder.BuildContents(
            root.transform,
            room,
            layout.RoomRandom(room, 505),
            allowChest: !state.ChestOpened
        );
        if (chest != null)
            chest.Opened += () => state.ChestOpened = true;

        for (int direction = 0; direction < 4; direction++)
        {
            DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
            if (!loadedEdges.ContainsKey(edge))
                loadedEdges[edge] = builder.BuildEdge(
                    transform,
                    edge,
                    layout.IsDoorOpen(room, direction)
                );
        }

        foreach (Vector2Int vertex in RoomVertices(room))
        {
            if (!loadedCorners.ContainsKey(vertex))
                loadedCorners[vertex] = builder.BuildCorner(transform, vertex);
        }

        var loaded = new LoadedRoom { Root = root, DormantEnemies = new List<EnemyBase>() };
        if (!state.Visited)
        {
            loaded.DormantEnemies = DungeonEnemyPlacer.SpawnDormant(enemyPrefabs, layout, room);
        }

        loadedRooms[room] = loaded;
    }

    private void UnloadDistantRooms()
    {
        List<Vector2Int> toUnload = null;
        foreach (KeyValuePair<Vector2Int, LoadedRoom> pair in loadedRooms)
        {
            Vector2Int delta = pair.Key - currentRoom;
            if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) > unloadDistance)
                (toUnload ??= new List<Vector2Int>()).Add(pair.Key);
        }
        if (toUnload == null)
            return;

        foreach (Vector2Int room in toUnload)
        {
            LoadedRoom loaded = loadedRooms[room];
            DungeonEnemyPlacer.Despawn(loaded.DormantEnemies);
            Destroy(loaded.Root);
            loadedRooms.Remove(room);
        }

        PruneSharedGeometry();
    }

    private void PruneSharedGeometry()
    {
        List<DungeonEdge> deadEdges = null;
        foreach (KeyValuePair<DungeonEdge, GameObject> pair in loadedEdges)
        {
            var roomA = new Vector2Int(pair.Key.X, pair.Key.Y);
            Vector2Int roomB = roomA + (pair.Key.Horizontal ? Vector2Int.up : Vector2Int.right);
            if (!loadedRooms.ContainsKey(roomA) && !loadedRooms.ContainsKey(roomB))
                (deadEdges ??= new List<DungeonEdge>()).Add(pair.Key);
        }
        if (deadEdges != null)
        {
            foreach (DungeonEdge edge in deadEdges)
            {
                Destroy(loadedEdges[edge]);
                loadedEdges.Remove(edge);
            }
        }

        List<Vector2Int> deadCorners = null;
        foreach (KeyValuePair<Vector2Int, GameObject> pair in loadedCorners)
        {
            bool anyLoaded = false;
            foreach (Vector2Int room in VertexRooms(pair.Key))
                anyLoaded |= loadedRooms.ContainsKey(room);
            if (!anyLoaded)
                (deadCorners ??= new List<Vector2Int>()).Add(pair.Key);
        }
        if (deadCorners != null)
        {
            foreach (Vector2Int vertex in deadCorners)
            {
                Destroy(loadedCorners[vertex]);
                loadedCorners.Remove(vertex);
            }
        }
    }

    private RoomState GetState(Vector2Int room)
    {
        if (!roomStates.TryGetValue(room, out RoomState state))
        {
            state = new RoomState();
            roomStates[room] = state;
        }
        return state;
    }

    /// <summary>The four grid vertices at this room's corners. Vertex (x, y)
    /// is the north-east corner of room (x, y).</summary>
    private static IEnumerable<Vector2Int> RoomVertices(Vector2Int room)
    {
        yield return room;
        yield return room + Vector2Int.left;
        yield return room + Vector2Int.down;
        yield return room + Vector2Int.left + Vector2Int.down;
    }

    private static IEnumerable<Vector2Int> VertexRooms(Vector2Int vertex)
    {
        yield return vertex;
        yield return vertex + Vector2Int.right;
        yield return vertex + Vector2Int.up;
        yield return vertex + Vector2Int.right + Vector2Int.up;
    }

    private static Transform ResolvePlayer()
    {
        GameContext context = GameContext.Instance;
        if (context != null && context.PlayerTransform != null)
            return context.PlayerTransform;

        PlayerController controller = FindAnyObjectByType<PlayerController>();
        return controller != null ? controller.transform : null;
    }

    private void LoadEnemyPrefabs()
    {
        enemyPrefabs.Clear();
        foreach (GameObject prefab in Resources.LoadAll<GameObject>(EnemyResourceFolder))
        {
            EnemyBase enemy = prefab != null ? prefab.GetComponent<EnemyBase>() : null;
            if (enemy != null)
                enemyPrefabs.Add(enemy);
        }
        enemyPrefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (enemyPrefabs.Count == 0)
            Debug.LogError($"DungeonManager: no enemy prefabs in '{EnemyResourceFolder}'.");
    }
}
