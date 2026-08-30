using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Walks a player along a path and records what the wall-visibility decision
/// did at every frame. Nothing here renders, animates, or waits for a frame:
/// the whole system under test is arithmetic over a camera, a path, and the
/// dungeon's planned geometry, so temporal behaviour can be asserted instead
/// of watched.
/// </summary>
internal static class WallVisibilitySimulation
{
    public const float FrameStep = 1f / 30f;

    // The Broccoli player and the coronavirus enemies, as they are built in
    // the Dungeon scene. A character the camera sees over a wall is not hidden
    // by it, so these have to be the sizes the game actually renders.
    public const float PlayerWidth = 1.36f;
    public const float PlayerHeight = 1f;
    public const float EnemyWidth = 1.1f;
    public const float EnemyHeight = 1f;

    /// <summary>The gameplay camera, as authored in the Dungeon scene.</summary>
    public readonly struct CameraConfig
    {
        public readonly Vector3 Offset;
        public readonly float FieldOfView;
        public readonly float Aspect;
        public readonly float NearClip;
        public readonly float FarClip;

        public CameraConfig(
            Vector3 offset,
            float fieldOfView,
            float aspect,
            float nearClip,
            float farClip
        )
        {
            Offset = offset;
            FieldOfView = fieldOfView;
            Aspect = aspect;
            NearClip = nearClip;
            FarClip = farClip;
        }

        public static CameraConfig Dungeon =>
            new(new Vector3(0f, 10.5f, -11.7f), 35f, 16f / 9f, 0.3f, 1000f);

        public CameraConfig WithOffset(Vector3 offset)
        {
            return new CameraConfig(offset, FieldOfView, Aspect, NearClip, FarClip);
        }

        public Quaternion Rotation => Quaternion.LookRotation(-Offset.normalized, Vector3.up);

        public OcclusionCameraModel At(Vector3 playerPosition)
        {
            return OcclusionCameraModel.Perspective(
                playerPosition + Offset,
                Rotation,
                FieldOfView,
                Aspect,
                NearClip,
                FarClip
            );
        }
    }

    public sealed class Frame
    {
        public int Index;
        public float Time;
        public Vector3 PlayerPosition;
        public Vector3 CameraPosition;
        public Vector2Int PlayerRoom;
        public Vector3 GroundForward;
        public float DeepestTargetDepth;
        public readonly List<Vector3> TargetPositions = new();
        public readonly List<int> LoweredGroups = new();
        public readonly Dictionary<int, WallVisibilityReason> Reasons = new();
        public readonly Dictionary<int, float> Coverage = new();
        public readonly HashSet<int> Activated = new();

        /// <summary>Pieces of lowered groups that fade this frame.</summary>
        public readonly HashSet<int> LoweredPieces = new();

        /// <summary>
        /// Pieces of lowered groups standing in the gap by raw geometry alone.
        /// A lowered piece outside this set is being held through a release by
        /// the per-piece hysteresis.
        /// </summary>
        public readonly HashSet<int> GapPieces = new();

        public bool IsLowered(int groupId)
        {
            return LoweredGroups.Contains(groupId);
        }
    }

    public sealed class Result
    {
        public WallVisibilityWorld World;
        public CameraConfig Camera;
        public IReadOnlyList<Vector3> Enemies;
        public readonly List<Frame> Frames = new();

        public int Seed => World.Seed;

        /// <summary>Every group that was lowered at some point in the run.</summary>
        public IEnumerable<int> TouchedGroups
        {
            get
            {
                var seen = new HashSet<int>();
                foreach (Frame frame in Frames)
                foreach (int groupId in frame.LoweredGroups)
                {
                    if (seen.Add(groupId))
                        yield return groupId;
                }
            }
        }
    }

    public static Result Run(
        WallVisibilityWorld world,
        IReadOnlyList<Vector3> path,
        CameraConfig camera,
        IReadOnlyList<Vector3> enemies = null
    )
    {
        var resolver = new WallVisibilityResolver();
        var result = new Result
        {
            World = world,
            Camera = camera,
            Enemies = enemies ?? System.Array.Empty<Vector3>(),
        };

        for (int index = 0; index < path.Count; index++)
        {
            float time = index * FrameStep;
            Vector3 player = path[index];
            OcclusionCameraModel model = camera.At(player);
            resolver.BeginFrame();
            AddTargets(resolver, model, player, result.Enemies, world.Block.Layout);
            resolver.Resolve(model, world, time);
            result.Frames.Add(Record(world, resolver, model, index, time, player));
        }
        return result;
    }

    private static void AddTargets(
        WallVisibilityResolver resolver,
        in OcclusionCameraModel camera,
        Vector3 player,
        IReadOnlyList<Vector3> enemies,
        DungeonLayout layout
    )
    {
        if (
            OcclusionTarget.TryCreate(
                camera,
                OcclusionTargetKind.Player,
                player,
                BodyBounds(player, PlayerWidth, PlayerHeight),
                0.5f,
                out OcclusionTarget playerTarget
            )
        )
            resolver.AddTarget(playerTarget);

        foreach (Vector3 enemy in enemies)
        {
            if (
                EnemyRevealGate.IsRevealed(player, enemy, layout)
                && OcclusionTarget.TryCreate(
                    camera,
                    OcclusionTargetKind.Enemy,
                    enemy,
                    BodyBounds(enemy, EnemyWidth, EnemyHeight),
                    0.05f,
                    out OcclusionTarget enemyTarget
                )
            )
                resolver.AddTarget(enemyTarget);
        }
    }

    public static Bounds BodyBounds(Vector3 position, float width, float height)
    {
        return new Bounds(position + Vector3.up * (height / 2f), new Vector3(width, height, width));
    }

    private static Frame Record(
        WallVisibilityWorld world,
        WallVisibilityResolver resolver,
        in OcclusionCameraModel camera,
        int index,
        float time,
        Vector3 player
    )
    {
        Vector3 groundForward = Vector3.ProjectOnPlane(camera.Forward, Vector3.up).normalized;
        var frame = new Frame
        {
            Index = index,
            Time = time,
            PlayerPosition = player,
            CameraPosition = camera.Position,
            PlayerRoom = DungeonLayout.RoomAt(new Vector2(player.x, player.z)),
            GroundForward = groundForward,
            DeepestTargetDepth = float.NegativeInfinity,
        };

        foreach (OcclusionTarget target in resolver.Targets)
        {
            frame.TargetPositions.Add(target.Position);
            frame.DeepestTargetDepth = Mathf.Max(
                frame.DeepestTargetDepth,
                Vector3.Dot(target.Position - camera.Position, groundForward)
            );
        }

        foreach (KeyValuePair<int, OcclusionActivation> activation in resolver.Activations)
        {
            frame.Coverage[activation.Key] = activation.Value.Coverage;
            frame.Activated.Add(activation.Key);
        }

        foreach (int groupId in resolver.LoweredGroups)
        {
            frame.LoweredGroups.Add(groupId);
            frame.Reasons[groupId] = resolver.ReasonFor(groupId);
            foreach (int pieceId in world.GroupOf(groupId).Pieces)
            {
                Bounds structure = world.PieceOf(pieceId).Structure;
                if (resolver.IsPieceInTheGap(structure))
                    frame.GapPieces.Add(pieceId);
                if (resolver.IsPieceInTheWay(pieceId, structure))
                    frame.LoweredPieces.Add(pieceId);
            }
        }
        return frame;
    }
}
