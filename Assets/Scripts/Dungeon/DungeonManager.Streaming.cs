using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonManager
{
    private static readonly Vector2Int[] NeighborhoodOrder =
    {
        Vector2Int.zero,
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.up + Vector2Int.right,
        Vector2Int.down + Vector2Int.right,
        Vector2Int.down + Vector2Int.left,
        Vector2Int.up + Vector2Int.left,
    };

    private readonly List<Vector2Int> unloadBuffer = new();
    private bool isRoomStreaming;
    private int streamingRevision;

    /// <summary>
    /// Streams the next neighbourhood over several frames, then updates the
    /// existing NavMesh asynchronously. The room just entered was prepared by
    /// the previous neighbourhood, so its enemies can wake immediately.
    /// </summary>
    private void RequestRoomStreaming()
    {
        streamingRevision++;
        if (isRoomStreaming)
            return;

        isRoomStreaming = true;
        StartCoroutine(StreamRooms());
    }

    private IEnumerator StreamRooms()
    {
        while (true)
        {
            int revision = streamingRevision;
            Vector2Int targetRoom = currentRoom;

            foreach (Vector2Int offset in NeighborhoodOrder)
            {
                if (revision != streamingRevision)
                    break;

                Vector2Int room = targetRoom + offset;
                if (loadedRooms.ContainsKey(room))
                    continue;

                EnsureRoom(room);
                yield return null;
            }

            if (revision != streamingRevision)
                continue;

            CollectDistantRooms(targetRoom);
            foreach (Vector2Int room in unloadBuffer)
            {
                if (!IsDistantFromCurrentRoom(room))
                    continue;

                UnloadRoom(room);
                yield return null;
            }
            unloadBuffer.Clear();
            PruneSharedGeometry();

            if (navMeshDirty)
            {
                navMeshDirty = false;
                AsyncOperation update = navSurface.UpdateNavMesh(navSurface.navMeshData);
                yield return update;

                foreach (LoadedRoom loaded in loadedRooms.Values)
                    DungeonEnemyPlacer.AlignToNavMesh(loaded.DormantEnemies);
            }

            if (revision == streamingRevision)
                break;
        }

        isRoomStreaming = false;
    }

    private void CollectDistantRooms(Vector2Int origin)
    {
        unloadBuffer.Clear();
        foreach (Vector2Int room in loadedRooms.Keys)
        {
            Vector2Int delta = room - origin;
            if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) > unloadDistance)
                unloadBuffer.Add(room);
        }
    }

    private bool IsDistantFromCurrentRoom(Vector2Int room)
    {
        Vector2Int delta = room - currentRoom;
        return Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) > unloadDistance;
    }

    private void UnloadRoom(Vector2Int room)
    {
        if (!loadedRooms.TryGetValue(room, out LoadedRoom loaded))
            return;

        DungeonEnemyPlacer.Despawn(loaded.DormantEnemies);
        Destroy(loaded.Root);
        loadedRooms.Remove(room);
        navMeshDirty = true;
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

        List<Vector2Int> deadJunctions = null;
        foreach (KeyValuePair<Vector2Int, GameObject> pair in loadedJunctions)
        {
            bool anyLoaded = false;
            foreach (Vector2Int room in VertexRooms(pair.Key))
                anyLoaded |= loadedRooms.ContainsKey(room);
            if (!anyLoaded)
                (deadJunctions ??= new List<Vector2Int>()).Add(pair.Key);
        }
        if (deadJunctions == null)
            return;

        foreach (Vector2Int vertex in deadJunctions)
        {
            Destroy(loadedJunctions[vertex]);
            loadedJunctions.Remove(vertex);
        }
    }
}
