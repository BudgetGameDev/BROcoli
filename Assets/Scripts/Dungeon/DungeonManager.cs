using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Runs the dungeon-crawler game mode: an infinite grid of procedurally
/// generated rooms joined by doorways. Rooms (geometry, props, loot, and a
/// dormant enemy group) are generated one step ahead of the player, so
/// whatever waits behind a doorway already exists before it is entered.
/// Far-away rooms unload; the deterministic seed rebuilds them identically
/// when the player backtracks.
/// </summary>
public partial class DungeonManager : MonoBehaviour
{
    private const string EnemyResourceFolder = "CursedDevolpmentStudioAss Assets/Waves";
    private const float RoomCheckInterval = 0.2f;

    [SerializeField]
    private DungeonRoomBuilder builder;

    [SerializeField]
    private DungeonPropPlacer decor;

    [SerializeField]
    [Tooltip("0 picks a fresh random seed every run.")]
    private int seed;

    [SerializeField, Min(1)]
    private int unloadDistance = 2;

    private sealed class RoomState
    {
        public bool Visited;
        public readonly HashSet<int> OpenedChestSlots = new();
    }

    private sealed class LoadedRoom
    {
        public GameObject Root;
        public List<EnemyBase> DormantEnemies;
    }

    private readonly Dictionary<Vector2Int, LoadedRoom> loadedRooms = new();
    private readonly Dictionary<DungeonEdge, GameObject> loadedEdges = new();
    private readonly Dictionary<Vector2Int, GameObject> loadedJunctions = new();
    private readonly Dictionary<Vector2Int, RoomState> roomStates = new();
    private readonly List<EnemyBase> enemyPrefabs = new();

    private DungeonLayout layout;
    private Transform player;
    private Vector2Int currentRoom;
    private bool hasCurrentRoom;
    private float nextRoomCheck;
    private NavMeshSurface navSurface;
    private bool navMeshDirty;

    public int Seed => seed;

    private void Start()
    {
        if (seed == 0)
            seed = Random.Range(1, int.MaxValue);
        layout = new DungeonLayout(seed);
        Debug.Log($"DungeonManager: generating dungeon with seed {seed}.");

        // Enemies path through doorways over a NavMesh baked from the loaded
        // rooms' render meshes (see DungeonEnemyNavigator).
        navSurface = gameObject.AddComponent<NavMeshSurface>();
        navSurface.collectObjects = CollectObjects.Children;
        navSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        // Open-gate arch meshes sit on Ignore Raycast so their low arch does
        // not carve the NavMesh out of the doorway (agent height > arch).
        navSurface.layerMask = ~(1 << 2);

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

        if (navSurface.navMeshData != null)
        {
            DungeonEnemyPlacer.Activate(loadedRooms[room].DormantEnemies);
            GetState(room).Visited = true;
            RequestRoomStreaming();
            return;
        }

        // The initial loading frame prepares everything around the spawn and
        // establishes the first NavMesh. Later room changes stream and update
        // incrementally so walking through a doorway never runs a full bake.
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
            EnsureRoom(room + new Vector2Int(dx, dy));

        navSurface.BuildNavMesh();
        navMeshDirty = false;
        foreach (LoadedRoom loaded in loadedRooms.Values)
            DungeonEnemyPlacer.AlignToNavMesh(loaded.DormantEnemies);

        DungeonEnemyPlacer.Activate(loadedRooms[room].DormantEnemies);
        GetState(room).Visited = true;
    }

    private void EnsureRoom(Vector2Int room)
    {
        if (loadedRooms.ContainsKey(room))
            return;

        RoomState state = GetState(room);
        DungeonLayout.RoomArchetype archetype = layout.Archetype(room);
        var root = new GameObject($"Room ({room.x}, {room.y}) [{archetype}]");
        root.transform.SetParent(transform, false);

        DungeonLayout.RoomDoorways doorways = layout.Doorways(room);
        builder.BuildFloor(root.transform, room, archetype, layout.RoomRandom(room, 404));
        builder.BuildInterior(root.transform, room, archetype);
        List<DungeonPropPlacer.PlacedChest> chests = decor.BuildContents(
            root.transform,
            room,
            archetype,
            doorways,
            layout.RoomRandom(room, 505),
            state.OpenedChestSlots
        );
        foreach (DungeonPropPlacer.PlacedChest placed in chests)
        {
            int slot = placed.Slot;
            placed.Chest.Opened += () => state.OpenedChestSlots.Add(slot);
        }
        decor.BuildAtmosphere(
            root.transform,
            room,
            archetype,
            doorways,
            layout.RoomRandom(room, 707)
        );

        for (int direction = 0; direction < 4; direction++)
        {
            DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
            if (!loadedEdges.ContainsKey(edge))
                loadedEdges[edge] = builder.BuildEdge(
                    transform,
                    edge,
                    layout.Passage(edge, layout.IsDoorOpen(room, direction))
                );
        }

        foreach (Vector2Int vertex in RoomVertices(room))
        {
            if (!loadedJunctions.ContainsKey(vertex))
                loadedJunctions[vertex] = builder.BuildJunction(transform, vertex);
        }

        var loaded = new LoadedRoom { Root = root, DormantEnemies = new List<EnemyBase>() };
        if (!state.Visited)
        {
            loaded.DormantEnemies = DungeonEnemyPlacer.SpawnDormant(
                enemyPrefabs,
                layout,
                room,
                archetype
            );
        }

        loadedRooms[room] = loaded;
        navMeshDirty = true;
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
