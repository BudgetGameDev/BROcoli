using System.Reflection;
using BudgetGameDev.Autoplay;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class BotReactionIntegrationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ANewProjectileObservationCannotSteerUntilItsReactionDelayMatures()
        {
            var host = new GameObject("Delayed tactical observation");
            host.SetActive(false);
            try
            {
                var bot = host.AddComponent<BotDriver>();
                bot.ConfigureReaction(
                    new AutoplayConfig
                    {
                        ReactionDelaySeconds = 0.2f,
                        ObservationIntervalSeconds = 0.1f,
                    }
                );
                var scheduler =
                    (DelayedCommandScheduler<BotDriver.TacticalSnapshot>)
                        typeof(BotDriver).GetField("reaction", Hidden).GetValue(bot);
                var danger = new BotSituation(
                    false,
                    float.PositiveInfinity,
                    0,
                    1,
                    true,
                    false,
                    float.PositiveInfinity,
                    float.PositiveInfinity
                );
                Assert.That(scheduler.TryObserve(0), Is.True);
                scheduler.Enqueue(
                    new BotDriver.TacticalSnapshot(danger, default, default, Vector2.up),
                    0
                );

                bot.AdvanceTactics(Vector2.zero, 0.19);
                Assert.That(
                    (Vector2)typeof(BotDriver).GetField("lastDodge", Hidden).GetValue(bot),
                    Is.EqualTo(Vector2.zero)
                );
                bot.AdvanceTactics(Vector2.zero, 0.201);
                Assert.That(
                    (Vector2)typeof(BotDriver).GetField("lastDodge", Hidden).GetValue(bot),
                    Is.EqualTo(Vector2.up)
                );
                Assert.That(
                    (BotIntent)typeof(BotDriver).GetField("tacticalIntent", Hidden).GetValue(bot),
                    Is.EqualTo(BotIntent.Dodge)
                );

                bot.ResetLifeNavigation();
                Assert.That(
                    scheduler.TryActivate(10, out _),
                    Is.False,
                    "a respawn cannot inherit the previous life's queued reaction"
                );
                Assert.That(
                    (Vector2)typeof(BotDriver).GetField("lastDodge", Hidden).GetValue(bot),
                    Is.EqualTo(Vector2.zero)
                );
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
