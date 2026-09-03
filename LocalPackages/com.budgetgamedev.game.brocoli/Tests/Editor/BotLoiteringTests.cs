using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The two rules that stop a run being spent going nowhere.
    ///
    /// An agent pacing a wall, or circling the middle of a room with orbs lying in
    /// it, is moving the whole time, so nothing that watches for a stationary agent
    /// ever fires. These pin what does: a window of walking judged on where it
    /// actually ended up, and a preference for carrying on the way it was already
    /// going when two ways round an obstacle score alike.
    /// </summary>
    public sealed class BotLoiteringTests
    {
        private const float Efficiency = 0.3f;

        /// <summary>
        /// Walking a long way to end up where it started is loitering; walking the
        /// same distance and arriving somewhere is not.
        /// </summary>
        [Test]
        public void WalkingThatGoesNowhereIsLoitering()
        {
            Assert.That(
                BotDriver.IsLoitering(12f, 0.5f, Efficiency),
                Is.True,
                "twelve metres of walking that ended half a metre away is pacing"
            );
            Assert.That(
                BotDriver.IsLoitering(12f, 9f, Efficiency),
                Is.False,
                "walking that arrived somewhere is not pacing"
            );
        }

        /// <summary>
        /// Standing nearly still is the stationary check's question. Judging it
        /// here would fire on a single tick spent turning around, and every turn a
        /// route makes would read as being stuck.
        /// </summary>
        [Test]
        public void TooLittleWalkingIsNotJudgedAtAll()
        {
            Assert.That(BotDriver.IsLoitering(1.5f, 0f, Efficiency), Is.False);
            Assert.That(BotDriver.IsLoitering(0f, 0f, Efficiency), Is.False);
        }

        /// <summary>
        /// Only the goals meant to take the agent somewhere are judged. Fighting is
        /// circling on purpose: an agent kiting at the edge of its weapon's range
        /// covers ground without meaning to leave, and reading that as being stuck
        /// would make it walk away from every fight it was winning.
        /// </summary>
        [Test]
        public void OnlyGoalsThatAreMeantToArriveSomewhereAreJudged()
        {
            Assert.That(BotDriver.IsJourney(BotIntent.Explore), Is.True);
            Assert.That(BotDriver.IsJourney(BotIntent.Loot), Is.True);
            Assert.That(BotDriver.IsJourney(BotIntent.Collect), Is.True);

            Assert.That(BotDriver.IsJourney(BotIntent.Engage), Is.False);
            Assert.That(BotDriver.IsJourney(BotIntent.Retreat), Is.False);
            Assert.That(BotDriver.IsJourney(BotIntent.Dodge), Is.False);
            Assert.That(BotDriver.IsJourney(BotIntent.Recover), Is.False);
            Assert.That(BotDriver.IsJourney(BotIntent.Waiting), Is.False);
        }

        /// <summary>
        /// Two equally good ways past an obstacle are settled by whichever way the
        /// agent was already going. Without that the choice comes down to a
        /// tie-break, and the agent alternates between them on the spot.
        /// </summary>
        [Test]
        public void TheWayItWasAlreadyGoingWinsATie()
        {
            Vector2 desired = Vector2.up;
            Vector2 left = new Vector2(-1f, 1f).normalized;
            Vector2 right = new Vector2(1f, 1f).normalized;

            float towardLeft = BotDriver.ScoreHeading(left, desired, left, 1f);
            float towardRight = BotDriver.ScoreHeading(right, desired, left, 1f);

            Assert.That(
                towardLeft,
                Is.GreaterThan(towardRight),
                "the agent turned back on itself instead of carrying on round"
            );
        }

        /// <summary>
        /// The bias is a preference, not a veto: a way that is genuinely clearer,
        /// or genuinely more direct, still wins on the tick it opens up. An agent
        /// that would not change its mind is just stuck in a different way.
        /// </summary>
        [Test]
        public void AClearlyBetterWayStillWins()
        {
            Vector2 desired = Vector2.up;
            Vector2 committed = Vector2.down;

            Assert.That(
                BotDriver.ScoreHeading(desired, desired, committed, 1f),
                Is.GreaterThan(BotDriver.ScoreHeading(committed, desired, committed, 1f)),
                "a direction straight at the target lost to one straight away from it"
            );
        }

        /// <summary>Before the agent has gone anywhere there is nothing to commit to.</summary>
        [Test]
        public void TheFirstStepHasNothingToCarryOn()
        {
            Vector2 desired = Vector2.up;

            Assert.That(
                BotDriver.ScoreHeading(desired, desired, Vector2.zero, 0.5f),
                Is.EqualTo(2.5f).Within(0.0001f)
            );
        }

        /// <summary>
        /// The whole rule end to end: a window of walking that arrives somewhere is
        /// left alone, and one that ends where it began costs the agent the
        /// destination it was failing to reach. Giving up at once is the point --
        /// waiting for four more unsticking manoeuvres is most of a short run.
        /// </summary>
        [Test]
        public void APacingAgentGivesUpOnWhereItWasGoing()
        {
            GameObject host = new("Loiter host");
            host.SetActive(false);
            try
            {
                BotDriver bot = host.AddComponent<BotDriver>();
                Set(bot, "hasExplorationRoom", true);
                Set(bot, "recoveryUntil", 0f);
                Set(bot, "loiterOrigin", Vector2.zero);
                Set(bot, "loiterTravelled", 40f);
                Set(bot, "nextLoiterCheck", 0f);

                // Fighting circles on purpose, so its window is thrown away unjudged.
                SetIntent(BotIntent.Engage);
                Invoke(bot, "TrackLoitering", Vector2.zero, 1f);
                Assert.That(Read<float>(bot, "loiterTravelled"), Is.Zero);
                Assert.That(
                    Read<bool>(bot, "hasExplorationRoom"),
                    Is.True,
                    "a fight was mistaken for pacing"
                );

                // A window still filling is not judged yet either.
                SetIntent(BotIntent.Explore);
                Set(bot, "nextLoiterCheck", float.MaxValue);
                Invoke(bot, "TrackLoitering", Vector2.zero, 3f);
                Assert.That(Read<float>(bot, "loiterTravelled"), Is.EqualTo(3f));

                // A window that got somewhere keeps the destination it is walking to.
                Set(bot, "nextLoiterCheck", 0f);
                Set(bot, "loiterTravelled", 9f);
                Invoke(bot, "TrackLoitering", new Vector2(8f, 0f), 0f);
                Assert.That(Read<bool>(bot, "hasExplorationRoom"), Is.True);

                // One that ended where it began loses it.
                Set(bot, "loiterOrigin", new Vector2(8f, 0f));
                Set(bot, "nextLoiterCheck", 0f);
                Set(bot, "loiterTravelled", 12f);
                Invoke(bot, "TrackLoitering", new Vector2(8.2f, 0f), 0f);
                Assert.That(
                    Read<bool>(bot, "hasExplorationRoom"),
                    Is.False,
                    "the agent is still walking at a place it cannot reach"
                );
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// What counts as a fight going somewhere. Only an enemy going down does:
        /// taking a hit is not progress, however much it feels like one, and reading
        /// it as progress is what let a run hold its weapon's range against a crowd
        /// it was not killing for a game-minute at a time.
        /// </summary>
        [Test]
        public void OnlyAnEnemyGoingDownCountsAsGettingSomewhereInAFight()
        {
            GameObject host = new("Combat progress host");
            host.SetActive(false);
            try
            {
                SetAutoplayActive(true);
                AutoplayFeatureLog.Reset();
                BotDriver bot = host.AddComponent<BotDriver>();
                Set(bot, "lastKills", 0);
                Set(bot, "lastProgress", 0f);

                Invoke(bot, "TrackCombatProgress");
                Assert.That(
                    Read<float>(bot, "lastProgress"),
                    Is.Zero,
                    "a fight with nothing dying in it has achieved nothing"
                );

                AutoplayFeatureLog.Record(AutoplayFeatures.EnemyKilled);
                Invoke(bot, "TrackCombatProgress");
                Assert.That(Read<float>(bot, "lastProgress"), Is.EqualTo(Time.time));
                Assert.That(Read<int>(bot, "lastKills"), Is.EqualTo(1));

                Set(bot, "lastProgress", 0f);
                Invoke(bot, "TrackCombatProgress");
                Assert.That(
                    Read<float>(bot, "lastProgress"),
                    Is.Zero,
                    "the same kill cannot count twice"
                );
            }
            finally
            {
                AutoplayFeatureLog.Reset();
                SetAutoplayActive(false);
                Object.DestroyImmediate(host);
            }
        }

        private static void SetAutoplayActive(bool active) =>
            typeof(AutoplayController)
                .GetField("<IsActive>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, active);

        private const BindingFlags Private =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static void SetIntent(BotIntent intent) =>
            typeof(BotDriver)
                .GetField("currentIntent", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, intent);

        private static void Set(object target, string name, object value) =>
            typeof(BotDriver).GetField(name, Private).SetValue(target, value);

        private static T Read<T>(object target, string name) =>
            (T)typeof(BotDriver).GetField(name, Private).GetValue(target);

        private static void Invoke(object target, string name, params object[] arguments) =>
            typeof(BotDriver).GetMethod(name, Private).Invoke(target, arguments);
    }
}
