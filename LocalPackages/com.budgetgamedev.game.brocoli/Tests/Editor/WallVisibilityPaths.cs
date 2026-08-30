using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The player paths the property tests walk: the targeted routes that exercise
    /// a named situation - alongside a wall, across a prefab seam, around a corner,
    /// through a doorway - and randomized walks that stay inside the space a
    /// player-sized capsule can actually reach.
    /// </summary>
    internal static class WallVisibilityPaths
    {
        public const float WalkSpeed = 4.5f;

        public static Vector3 Ground(Vector2 point)
        {
            return new Vector3(point.x, 0f, point.y);
        }

        /// <summary>Frames walking through the waypoints at a steady speed.</summary>
        public static List<Vector3> Polyline(params Vector3[] waypoints)
        {
            return Polyline(WalkSpeed, waypoints);
        }

        public static List<Vector3> Polyline(float speed, params Vector3[] waypoints)
        {
            var path = new List<Vector3>();
            float step = Mathf.Max(0.001f, speed * WallVisibilitySimulation.FrameStep);
            path.Add(waypoints[0]);
            for (int i = 1; i < waypoints.Length; i++)
            {
                float distance = Vector3.Distance(waypoints[i - 1], waypoints[i]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(distance / step));
                for (int s = 1; s <= steps; s++)
                    path.Add(Vector3.Lerp(waypoints[i - 1], waypoints[i], s / (float)steps));
            }
            return path;
        }

        /// <summary>Standing still, so hysteresis can be observed on its own.</summary>
        public static List<Vector3> Hold(Vector3 position, float seconds)
        {
            var path = new List<Vector3>();
            int frames = Mathf.CeilToInt(seconds / WallVisibilitySimulation.FrameStep);
            for (int i = 0; i < frames; i++)
                path.Add(position);
            return path;
        }

        public static List<Vector3> Concat(params IReadOnlyList<Vector3>[] parts)
        {
            var path = new List<Vector3>();
            foreach (IReadOnlyList<Vector3> part in parts)
                path.AddRange(part);
            return path;
        }

        /// <summary>
        /// A walk that never leaves the space a player capsule fits in, so a random
        /// path is a path the game could actually produce.
        /// </summary>
        public static List<Vector3> RandomWalk(WallVisibilityWorld world, int seed, int frames)
        {
            DungeonGeometryModel block = world.Block;
            DungeonWalkableSpace space = new(
                Domain(block),
                block.Walls,
                DungeonGeometryModel.PlayerRadius
            );
            var random = new System.Random(seed);
            Vector2 position = FreeStart(space, block);
            Vector2 direction = RandomDirection(random);
            float step = WalkSpeed * WallVisibilitySimulation.FrameStep;

            var path = new List<Vector3>(frames);
            for (int i = 0; i < frames; i++)
            {
                Vector2 next = position + direction * step;
                for (int attempt = 0; attempt < 24 && !space.IsFree(next); attempt++)
                {
                    direction = RandomDirection(random);
                    next = position + direction * step;
                }
                if (space.IsFree(next))
                    position = next;
                if (random.NextDouble() < 0.05)
                    direction = RandomDirection(random);
                path.Add(Ground(position));
            }
            return path;
        }

        public static Rect Domain(DungeonGeometryModel block)
        {
            Rect domain = DungeonRoomGeometry.RoomFloorBounds(block.Rooms[0]);
            foreach (Vector2Int room in block.Rooms)
            {
                Rect bounds = DungeonRoomGeometry.RoomFloorBounds(room);
                domain = Rect.MinMaxRect(
                    Mathf.Min(domain.xMin, bounds.xMin),
                    Mathf.Min(domain.yMin, bounds.yMin),
                    Mathf.Max(domain.xMax, bounds.xMax),
                    Mathf.Max(domain.yMax, bounds.yMax)
                );
            }
            return domain;
        }

        private static Vector2 FreeStart(DungeonWalkableSpace space, DungeonGeometryModel block)
        {
            foreach (Vector2Int room in block.Rooms)
            {
                Vector2 center = DungeonLayout.RoomCenter(room);
                if (space.IsFree(center))
                    return center;
            }
            return DungeonLayout.RoomCenter(block.Rooms[0]);
        }

        private static Vector2 RandomDirection(System.Random random)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
