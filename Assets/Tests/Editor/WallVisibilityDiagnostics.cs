using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Turns a failing simulation frame into everything needed to diagnose it
/// without a screenshot: the seed and positions that reproduce it, the state
/// it came from, and the identity, room, coverage, and reason of every wall
/// group involved.
/// </summary>
internal static class WallVisibilityDiagnostics
{
    public static string Report(
        WallVisibilitySimulation.Result result,
        int frameIndex,
        string message,
        IEnumerable<int> highlightGroups = null
    )
    {
        WallVisibilitySimulation.Frame frame = result.Frames[frameIndex];
        WallVisibilitySimulation.Frame previous =
            frameIndex > 0 ? result.Frames[frameIndex - 1] : null;
        var report = new StringBuilder();
        report.AppendLine(message);
        report.AppendLine($"  seed            {result.Seed}");
        report.AppendLine($"  frame           {frame.Index} at t={frame.Time:0.000}s");
        report.AppendLine(
            $"  player          {Format(frame.PlayerPosition)} in room {frame.PlayerRoom}"
        );
        report.AppendLine($"  camera          {Format(frame.CameraPosition)}");
        report.AppendLine($"  camera forward  {Format(frame.GroundForward)}");
        report.AppendLine($"  deepest target  {frame.DeepestTargetDepth:0.000}");
        report.AppendLine($"  targets         {Join(frame.TargetPositions)}");
        report.AppendLine($"  enemies         {Join(result.Enemies)}");
        report.AppendLine(
            $"  previous state  {(previous == null ? "(none)" : Groups(result, previous.LoweredGroups))}"
        );
        report.AppendLine($"  lowered groups  {Groups(result, frame.LoweredGroups)}");
        if (highlightGroups != null)
            report.AppendLine($"  groups in question {Groups(result, highlightGroups)}");

        foreach (int groupId in frame.LoweredGroups)
            AppendGroup(report, result, frame, groupId);
        if (highlightGroups != null)
        {
            foreach (int groupId in highlightGroups)
            {
                if (!frame.IsLowered(groupId))
                    AppendGroup(report, result, frame, groupId);
            }
        }
        return report.ToString();
    }

    private static void AppendGroup(
        StringBuilder report,
        WallVisibilitySimulation.Result result,
        WallVisibilitySimulation.Frame frame,
        int groupId
    )
    {
        WallVisibilityWorld.Group group = result.World.GroupOf(groupId);
        float coverage = frame.Coverage.TryGetValue(groupId, out float value) ? value : 0f;
        WallVisibilityReason reason = frame.Reasons.TryGetValue(groupId, out WallVisibilityReason r)
            ? r
            : WallVisibilityReason.NotOccluding;
        report.AppendLine(
            $"  group {groupId} \"{group.Name}\" room {group.Room} kind {group.Kind}"
                + $" state {(frame.IsLowered(groupId) ? "LOWERED" : "FULL")}"
                + $" reason {reason} coverage {coverage:0.000}"
        );
        foreach (int pieceId in group.Pieces)
        {
            WallVisibilityWorld.Piece piece = result.World.PieceOf(pieceId);
            report.AppendLine(
                $"      piece {pieceId} {piece.Label} "
                    + $"{(frame.LoweredPieces.Contains(pieceId) ? "lowered" : "standing")}"
            );
        }
    }

    private static string Groups(WallVisibilitySimulation.Result result, IEnumerable<int> groupIds)
    {
        var text = new StringBuilder();
        foreach (int groupId in groupIds)
        {
            if (text.Length > 0)
                text.Append(", ");
            text.Append($"{groupId}:\"{result.World.GroupOf(groupId).Name}\"");
        }
        return text.Length == 0 ? "(none)" : text.ToString();
    }

    private static string Join(IEnumerable<Vector3> positions)
    {
        var text = new StringBuilder();
        foreach (Vector3 position in positions)
        {
            if (text.Length > 0)
                text.Append(", ");
            text.Append(Format(position));
        }
        return text.Length == 0 ? "(none)" : text.ToString();
    }

    private static string Format(Vector3 value)
    {
        return $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";
    }
}
