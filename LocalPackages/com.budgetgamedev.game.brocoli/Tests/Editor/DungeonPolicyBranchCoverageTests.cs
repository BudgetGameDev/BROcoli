using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonPolicyBranchCoverageTests
    {
        private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void WallGroupUnionCompressesChainsAndSupportsReverseLexicalRoots()
        {
            var chain = new Dictionary<string, string>
            {
                ["c"] = "c",
                ["b"] = "c",
                ["a"] = "b",
            };
            MethodInfo find = typeof(DungeonWallGrouping).GetMethod("Find", HiddenStatic);
            Assert.That(find.Invoke(null, new object[] { chain, "a" }), Is.EqualTo("c"));
            Assert.That(chain["a"], Is.EqualTo("c"));

            var reverse = new Dictionary<string, string> { ["z"] = "z", ["a"] = "a" };
            typeof(DungeonWallGrouping)
                .GetMethod("Union", HiddenStatic)
                .Invoke(null, new object[] { reverse, "z", "a" });
            Assert.That(reverse["z"], Is.EqualTo("a"));

            var forward = new Dictionary<string, string> { ["a"] = "a", ["z"] = "z" };
            typeof(DungeonWallGrouping)
                .GetMethod("Union", HiddenStatic)
                .Invoke(null, new object[] { forward, "a", "z" });
            Assert.That(forward["z"], Is.EqualTo("a"));

            Assert.That(DungeonLayout.PickNthDirectionBit(0, 0), Is.Zero);
            Assert.That(DungeonLayout.PickNthDirectionBit(1, 2), Is.Zero);
            var layout = new DungeonLayout(17);
            Assert.That(
                typeof(DungeonLayout)
                    .GetMethod("PickDirectionBit", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(layout, new object[] { default(UnityEngine.Vector2Int), 1, 0 }),
                Is.EqualTo(0)
            );
        }
    }
}
