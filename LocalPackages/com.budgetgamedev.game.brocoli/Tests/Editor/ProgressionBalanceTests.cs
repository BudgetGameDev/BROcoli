using System.Collections.Generic;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The balance verdict is the harness's statement about difficulty, and a bare
    /// pass or fail would be useless for tuning, so what matters is that each band
    /// it leaves is named along with the direction it left in.
    /// </summary>
    public sealed class ProgressionBalanceTests
    {
        /// <summary>A run sitting in the middle of every band, for tests to bend.</summary>
        private static ProgressionSummary Balanced(
            float secondsPerLevel = 60f,
            float earlySecondsPerLevel = 40f,
            float lateSecondsPerLevel = 80f,
            float meanHealth = 0.7f,
            float dangerShare = 0.1f,
            int deaths = 2,
            float duration = 900f,
            int levels = 9
        ) =>
            new(
                levels + 1,
                levels,
                deaths + 1,
                deaths,
                duration,
                secondsPerLevel,
                earlySecondsPerLevel,
                lateSecondsPerLevel,
                5f,
                14f,
                meanHealth,
                0.2f,
                dangerShare,
                0.3f
            );

        private static ScalingSummary Scaled(
            int rooms = 24,
            int maxRing = 4,
            float firstHealthScale = 1f,
            float peakHealthScale = 2.4f
        ) => new(rooms, maxRing, 90, 12, 3.1f, firstHealthScale, peakHealthScale, 1.6f);

        /// <summary>Whether any finding mentions a phrase, named for readable failures.</summary>
        private static bool Mentions(List<string> findings, string phrase) =>
            findings.Exists(finding => finding.Contains(phrase));

        [Test]
        public void ARunInsideEveryBandReportsNothingAndPasses()
        {
            Assert.That(ProgressionBalance.Evaluate(Balanced(), Scaled()), Is.Empty);
            Assert.That(ProgressionBalance.Passed(Balanced(), Scaled()), Is.True);
        }

        [Test]
        public void ARunTooShortOrTooShallowToJudgeSaysSoInsteadOfGuessing()
        {
            List<string> brief = ProgressionBalance.Evaluate(Balanced(duration: 60f), Scaled());
            Assert.That(brief, Has.Count.EqualTo(1));
            Assert.That(brief[0], Does.Contain("too short to judge"));

            List<string> shallow = ProgressionBalance.Evaluate(Balanced(levels: 2), Scaled());
            Assert.That(shallow, Has.Count.EqualTo(1));
            Assert.That(shallow[0], Does.Contain("too few levels to judge"));
        }

        [Test]
        public void LevellingFasterOrSlowerThanTheBandIsNamedWithItsDirection()
        {
            Assert.That(
                ProgressionBalance.Evaluate(Balanced(secondsPerLevel: 5f), Scaled())[0],
                Does.Contain("level pace too low").And.Contain("too fast to be worth choosing")
            );
            Assert.That(
                ProgressionBalance.Evaluate(Balanced(secondsPerLevel: 400f), Scaled())[0],
                Does.Contain("level pace too high").And.Contain("grind")
            );
        }

        [Test]
        public void ACurveThatNeverSteepensAndOneThatWallsAreBothReported()
        {
            Assert.That(
                ProgressionBalance.Evaluate(
                    Balanced(earlySecondsPerLevel: 80f, lateSecondsPerLevel: 40f),
                    Scaled()
                )[0],
                Does.Contain("curve shape too low").And.Contain("no dearer")
            );
            Assert.That(
                ProgressionBalance.Evaluate(
                    Balanced(earlySecondsPerLevel: 20f, lateSecondsPerLevel: 300f),
                    Scaled()
                )[0],
                Does.Contain("curve shape too high").And.Contain("walls")
            );
        }

        [Test]
        public void ARunNothingThreatensAndOneFoughtAtTheEdgeAreBothReported()
        {
            List<string> idle = ProgressionBalance.Evaluate(
                Balanced(meanHealth: 0.99f, dangerShare: 0f),
                Scaled()
            );
            Assert.That(Mentions(idle, "nothing in the dungeon threatens the player"), Is.True);
            Assert.That(Mentions(idle, "the run is never in trouble"), Is.True);

            List<string> punishing = ProgressionBalance.Evaluate(
                Balanced(meanHealth: 0.2f, dangerShare: 0.8f),
                Scaled()
            );
            Assert.That(Mentions(punishing, "at the edge of death"), Is.True);
            Assert.That(Mentions(punishing, "in trouble more often than not"), Is.True);
        }

        [Test]
        public void ARunThatCannotBeLostAndOneLostConstantlyAreBothReported()
        {
            Assert.That(
                ProgressionBalance.Evaluate(Balanced(deaths: 0), Scaled())[0],
                Does.Contain("deaths too low").And.Contain("cannot be lost")
            );
            Assert.That(
                ProgressionBalance.Evaluate(Balanced(deaths: 40), Scaled())[0],
                Does.Contain("deaths too high").And.Contain("faster than it can be learned")
            );
        }

        [Test]
        public void ScalingIsGradedSeparatelyBecauseItFailsQuietly()
        {
            Assert.That(
                Mentions(
                    ProgressionBalance.Evaluate(Balanced(), Scaled(rooms: 0)),
                    "no room ever spawned enemies"
                ),
                Is.True
            );
            Assert.That(
                Mentions(
                    ProgressionBalance.Evaluate(Balanced(), Scaled(maxRing: 1)),
                    "never left ring 1"
                ),
                Is.True
            );
            Assert.That(
                Mentions(
                    ProgressionBalance.Evaluate(
                        Balanced(),
                        Scaled(firstHealthScale: 1.5f, peakHealthScale: 1.5f)
                    ),
                    "enemy health never scaled"
                ),
                Is.True
            );
        }

        [Test]
        public void ScalingGrowthIsReadAgainstTheRunsOwnFirstRoom()
        {
            Assert.That(
                Scaled(firstHealthScale: 1.5f, peakHealthScale: 3f).HealthScaleGrowth,
                Is.EqualTo(2f).Within(0.001f)
            );
        }
    }
}
