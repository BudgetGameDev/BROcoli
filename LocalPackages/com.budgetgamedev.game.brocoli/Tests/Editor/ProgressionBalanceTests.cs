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
            float secondsPerRing = 90f,
            float meanHealth = 0.7f,
            float dangerShare = 0.1f,
            int deaths = 2,
            float duration = 900f,
            int levels = 9
        ) =>
            new(
                new LevelPacing(
                    levels + 1,
                    levels,
                    secondsPerLevel,
                    earlySecondsPerLevel,
                    lateSecondsPerLevel,
                    5f,
                    14f
                ),
                new DepthPacing(4, 4, secondsPerRing),
                new HealthPressure(meanHealth, 0.2f, dangerShare, 0.3f),
                deaths + 1,
                deaths,
                duration
            );

        /// <summary>
        /// A dungeon that scaled its rooms sensibly, built out of the same samples the
        /// spawn path records, so the fixture cannot drift from what a run produces.
        /// </summary>
        private static ScalingSummary Scaled(
            int rooms = 24,
            int maxRing = 4,
            float firstHealthScale = 1f,
            float peakHealthScale = 2.4f,
            float peakDamageScale = 1.6f,
            float peakPlayerPower = 4.2f,
            int saturatedRooms = 0
        )
        {
            var samples = new List<ScalingSample>();
            for (int room = 0; room < rooms; room++)
            {
                bool last = room == rooms - 1;
                samples.Add(
                    new ScalingSample(
                        last ? maxRing : 1,
                        last ? peakPlayerPower : 1f,
                        1f,
                        last ? peakHealthScale : firstHealthScale,
                        last ? peakDamageScale : 1f,
                        // Saturating the headcount leaves health and damage growth
                        // alone, so lost headroom can be tested on its own.
                        room > 0
                        && room <= saturatedRooms
                            ? EnemyScaling.MaxCountPowerScale
                            : 1f,
                        1f,
                        last ? 12 : 4
                    )
                );
            }
            return ScalingSummary.Of(samples);
        }

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
        public void ARunThatSprintsPastTheRingLadderAndOneThatNeverLeavesAreBothReported()
        {
            Assert.That(
                Mentions(
                    ProgressionBalance.Evaluate(Balanced(secondsPerRing: 5f), Scaled()),
                    "sprints past the ring ladder"
                ),
                Is.True
            );
            Assert.That(
                Mentions(
                    ProgressionBalance.Evaluate(Balanced(secondsPerRing: 600f), Scaled()),
                    "never pushes out of the rings it started in"
                ),
                Is.True
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
        public void ScalingThatWentUnmeasuredSaysSoRatherThanBeingGradedOnNothing()
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
                    ProgressionBalance.Evaluate(Balanced(), Scaled(peakPlayerPower: 1.2f)),
                    "nothing for scaling to answer"
                ),
                Is.True
            );
        }

        [Test]
        public void ADungeonThatNeverGotTougherAndOneThatRanAwayAreBothReported()
        {
            Assert.That(
                Mentions(
                    ProgressionBalance.Evaluate(Balanced(), Scaled(peakHealthScale: 1.1f)),
                    "the deepest room is the first room with different furniture"
                ),
                Is.True
            );
            Assert.That(
                Mentions(
                    ProgressionBalance.Evaluate(Balanced(), Scaled(peakHealthScale: 9f)),
                    "enemy health outgrows"
                ),
                Is.True
            );
            Assert.That(
                Mentions(
                    ProgressionBalance.Evaluate(Balanced(), Scaled(peakDamageScale: 1f)),
                    "longer without making any of them dangerous"
                ),
                Is.True
            );
        }

        /// <summary>
        /// The measurement the rest of scaling exists to support. Health and damage can
        /// both grow on schedule and still leave a run trivial, if the build they are
        /// answering grew faster than either of them.
        /// </summary>
        [Test]
        public void ADungeonThatFellBehindThePlayerAndOneThatMatchedThemAreBothReported()
        {
            Assert.That(
                Mentions(
                    ProgressionBalance.Evaluate(
                        Balanced(),
                        Scaled(peakHealthScale: 1.6f, peakDamageScale: 1.25f, peakPlayerPower: 12f)
                    ),
                    "the player outgrows the dungeon"
                ),
                Is.True
            );
            Assert.That(
                Mentions(
                    ProgressionBalance.Evaluate(
                        Balanced(),
                        Scaled(peakHealthScale: 3f, peakDamageScale: 2.6f, peakPlayerPower: 3f)
                    ),
                    "every upgrade is answered in full"
                ),
                Is.True
            );
        }

        [Test]
        public void ARunSpentPinnedAgainstAScalingCeilingIsReportedAsLostHeadroom()
        {
            Assert.That(
                Mentions(
                    ProgressionBalance.Evaluate(Balanced(), Scaled(saturatedRooms: 20)),
                    "scaling headroom too low"
                ),
                Is.True
            );
            Assert.That(
                ProgressionBalance.Evaluate(Balanced(), Scaled(saturatedRooms: 4)),
                Is.Empty,
                "a run that only brushes a ceiling has not stopped scaling"
            );
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
    }
}
