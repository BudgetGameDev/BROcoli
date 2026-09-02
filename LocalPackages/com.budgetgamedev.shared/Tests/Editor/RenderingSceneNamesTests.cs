using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class RenderingSceneNamesTests
    {
        [Test]
        public void EachPipelineHasItsOwnSuffix()
        {
            Assert.That(
                RenderingSceneNames.SuffixFor(RenderPipelineKind.Universal),
                Is.EqualTo("_URP")
            );
            Assert.That(
                RenderingSceneNames.SuffixFor(RenderPipelineKind.HighDefinition),
                Is.EqualTo("_HDRP")
            );
            Assert.That(
                RenderingSceneNames.SuffixFor(RenderPipelineKind.Unknown),
                Is.Null,
                "A pipeline nobody recognizes has no rendering scene to bring in."
            );
        }

        [Test]
        public void TheLevelNameSurvivesEverySuffix()
        {
            Assert.That(
                RenderingSceneNames.LevelOf("Brocoli_Dungeon_Common"),
                Is.EqualTo("Brocoli_Dungeon")
            );
            Assert.That(
                RenderingSceneNames.LevelOf("Brocoli_Dungeon_URP"),
                Is.EqualTo("Brocoli_Dungeon")
            );
            Assert.That(
                RenderingSceneNames.LevelOf("Brocoli_Dungeon_HDRP"),
                Is.EqualTo("Brocoli_Dungeon")
            );
            Assert.That(
                RenderingSceneNames.LevelOf("Brocoli_Dungeon"),
                Is.EqualTo("Brocoli_Dungeon"),
                "A bare level name is already the level name."
            );
        }

        [Test]
        public void ARenderingSceneResolvesFromEitherTheLevelOrItsCommonScene()
        {
            Assert.That(
                RenderingSceneNames.RenderingSceneFor(
                    "Brocoli_Dungeon_Common",
                    RenderPipelineKind.HighDefinition
                ),
                Is.EqualTo("Brocoli_Dungeon_HDRP")
            );
            Assert.That(
                RenderingSceneNames.RenderingSceneFor(
                    "Brocoli_Dungeon",
                    RenderPipelineKind.Universal
                ),
                Is.EqualTo("Brocoli_Dungeon_URP")
            );
        }

        [Test]
        public void AnUnknownPipelineAsksForNoRenderingScene()
        {
            Assert.That(
                RenderingSceneNames.RenderingSceneFor(
                    "Brocoli_Dungeon_Common",
                    RenderPipelineKind.Unknown
                ),
                Is.Null
            );
        }

        [Test]
        public void TheCommonSceneIsReachableFromAnyOfTheThree()
        {
            Assert.That(
                RenderingSceneNames.CommonSceneFor("Brocoli_Dungeon_HDRP"),
                Is.EqualTo("Brocoli_Dungeon_Common")
            );
            Assert.That(
                RenderingSceneNames.CommonSceneFor("Brocoli_Dungeon_Common"),
                Is.EqualTo("Brocoli_Dungeon_Common"),
                "Resolving a common scene's own name must not stack a second suffix."
            );
        }
    }
}
