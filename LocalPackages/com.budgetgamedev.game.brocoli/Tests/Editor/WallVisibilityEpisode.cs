using System.Collections.Generic;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// One unbroken stretch of a wall group being lowered. Transitions are what
    /// the player actually sees, so the temporal properties are stated over these
    /// rather than over individual frames.
    /// </summary>
    internal readonly struct WallVisibilityEpisode
    {
        public readonly int StartFrame;
        public readonly int EndFrame;
        public readonly float Start;
        public readonly float End;

        public WallVisibilityEpisode(int startFrame, int endFrame, float start, float end)
        {
            StartFrame = startFrame;
            EndFrame = endFrame;
            Start = start;
            End = end;
        }

        public float Duration => End - Start + WallVisibilitySimulation.FrameStep;

        public static List<WallVisibilityEpisode> Of(
            WallVisibilitySimulation.Result result,
            int groupId
        )
        {
            var episodes = new List<WallVisibilityEpisode>();
            int startFrame = -1;
            for (int index = 0; index < result.Frames.Count; index++)
            {
                bool lowered = result.Frames[index].IsLowered(groupId);
                if (lowered && startFrame < 0)
                    startFrame = index;
                else if (!lowered && startFrame >= 0)
                {
                    episodes.Add(Build(result, startFrame, index - 1));
                    startFrame = -1;
                }
            }
            if (startFrame >= 0)
                episodes.Add(Build(result, startFrame, result.Frames.Count - 1));
            return episodes;
        }

        private static WallVisibilityEpisode Build(
            WallVisibilitySimulation.Result result,
            int startFrame,
            int endFrame
        )
        {
            return new WallVisibilityEpisode(
                startFrame,
                endFrame,
                result.Frames[startFrame].Time,
                result.Frames[endFrame].Time
            );
        }
    }
}
