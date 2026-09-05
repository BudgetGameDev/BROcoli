using System.IO;
using BudgetGameDev.Hub.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace BudgetGameDev.Hub.Tests
{
    public sealed class BuildContentPolicyTests
    {
        [Test]
        public void SourceProjectCannotProduceUnisolatedRelease()
        {
            Assume.That(File.Exists(BuildContentPolicy.StageFile), Is.False);
            Assert.Throws<BuildFailedException>(() => BuildContentPolicy.Validate(false));
        }

        [Test]
        public void SourceProjectStillAllowsDevelopmentPlayers()
        {
            Assume.That(File.Exists(BuildContentPolicy.StageFile), Is.False);
            Assert.DoesNotThrow(() => BuildContentPolicy.Validate(true));
        }

        [Test]
        public void CustomBuildPipelineCallerIsAlsoGated()
        {
            Assume.That(File.Exists(BuildContentPolicy.StageFile), Is.False);
            Assert.Throws<BuildFailedException>(() =>
                new BuildContentGate().OnFilterAssemblies(
                    BuildOptions.None,
                    new[] { "BudgetGameDev.Games.Brocoli.dll" }
                )
            );
        }
    }
}
