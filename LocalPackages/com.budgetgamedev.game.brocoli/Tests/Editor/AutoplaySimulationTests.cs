using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class AutoplaySimulationTests
    {
        [TestCase(50f, -100f)]
        [TestCase(50f, 0f)]
        [TestCase(50f, 100f)]
        public void SaveJourneyFatalHitIsPositiveAndLethalEvenWithNegativeArmor(
            float health,
            float armor
        )
        {
            float hit = AutoplaySaveJourneyDirector.FatalHitAmount(health, armor);
            Assert.That(hit, Is.GreaterThan(0f), "the damage handler rejects nonpositive hits");
            Assert.That(Mathf.Max(0f, hit - armor), Is.GreaterThan(health));
        }

        [Test]
        public void CaptureIsEnabledByDefaultAndExplicitlyDisabledForSimulation()
        {
            Assert.That(
                AutoplayConfig.FromArguments(new[] { "--autoplay" }, _ => null).CaptureEnabled,
                Is.True
            );
            var config = AutoplayConfig.FromArguments(
                new[] { "--autoplay", "--no-capture" },
                _ => null
            );
            Assert.That(config.CaptureEnabled, Is.False);
            var request = Editor.AutoplayRunRequest.FromArguments(
                new[] { "-no-capture" },
                () => ""
            );
            Assert.That(
                Editor.AutoplayRunner.PlayerArguments(request),
                Does.Contain("--no-capture")
            );

            Assert.That(
                Editor.AutoplayRunner.DescribeCaptures(
                    new Editor.AutoplayRunner.RunSummary { captureEnabled = false },
                    System.Globalization.CultureInfo.InvariantCulture
                ),
                Does.Contain("disabled").And.Contain("visual validation not performed")
            );

            var host = new GameObject("Simulation capture boundary");
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<AutoplayController>();
                Assert.That(controller.ConfigureCapture(config), Is.Null);
                Assert.That(host.GetComponent<FrameCapture>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(12345)]
        [TestCase(117074)]
        [TestCase(221803)]
        public void DistantFrontierIsReachedThroughAdjacentOpenDoorsInsteadOfAnUnloadedTarget(
            int seed
        )
        {
            var layout = new DungeonLayout(seed);
            Vector2Int start = layout.ClampToPlayableBand(new Vector2Int(4, 4));
            var visited = new HashSet<Vector2Int> { start };
            var frontier = new HashSet<Vector2Int> { start };
            // Three cleared doorways around the bot force the desired room beyond
            // the immediately streamed neighbourhood, as in the stalled seed.
            for (int depth = 0; depth < 3; depth++)
            {
                var next = new HashSet<Vector2Int>();
                foreach (Vector2Int room in frontier)
                    for (int direction = 0; direction < 4; direction++)
                        if (layout.IsPlayableDoorOpen(room, direction))
                        {
                            Vector2Int candidate = room + DungeonLayout.DirectionOffsets[direction];
                            if (visited.Add(candidate))
                                next.Add(candidate);
                        }
                frontier = next;
            }

            Vector2Int current = start;
            for (int hop = 0; hop < 5 && visited.Contains(current); hop++)
            {
                Assert.That(
                    BotExplorationPolicy.TryFindFrontier(
                        layout,
                        current,
                        visited,
                        1f,
                        out Vector2Int target,
                        out Vector2Int firstStep
                    ),
                    Is.True
                );
                Assert.That(visited.Contains(target), Is.False);
                Vector2Int offset = firstStep - current;
                int direction = System.Array.IndexOf(DungeonLayout.DirectionOffsets, offset);
                Assert.That(
                    direction,
                    Is.GreaterThanOrEqualTo(0),
                    "navigation must target a streamed adjacent room"
                );
                Assert.That(
                    layout.IsPlayableDoorOpen(current, direction),
                    Is.True,
                    "the first hop must follow the connected route"
                );
                current = firstStep;
            }
            Assert.That(
                visited.Contains(current),
                Is.False,
                "the route must escape the cleared pocket rather than oscillate within it"
            );
        }
    }
}
