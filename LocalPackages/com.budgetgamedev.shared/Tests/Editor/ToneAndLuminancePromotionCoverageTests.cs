using System.Reflection;
using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class ToneAndLuminancePromotionCoverageTests
    {
        [Test]
        public void ToneScaleCorrectionEscapesAZeroOutput()
        {
            MethodInfo correct = typeof(AcesToneScale).GetMethod(
                "Correct",
                BindingFlags.Static | BindingFlags.NonPublic
            );

            Assert.That(
                (float)correct.Invoke(null, new object[] { 0f, 0f, 1f }),
                Is.GreaterThan(0f)
            );
        }

        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void EveryAcesPresetReportsItsNominalPeak()
        {
            Assert.That(
                AcesToneScale.PresetPeakNits(HDRRangeReduction.ACES1000Nits),
                Is.EqualTo(1000f)
            );
            Assert.That(
                AcesToneScale.PresetPeakNits(HDRRangeReduction.ACES2000Nits),
                Is.EqualTo(2000f)
            );
            Assert.That(
                AcesToneScale.PresetPeakNits(HDRRangeReduction.ACES4000Nits),
                Is.EqualTo(4000f)
            );
        }

        [Test]
        public void InvalidCalibrationInputsUseSafeToneDefaults()
        {
            Assert.That(
                AcesToneScale.SelectPreset(float.PositiveInfinity),
                Is.EqualTo(HDRRangeReduction.ACES1000Nits)
            );
            Assert.That(
                AcesToneScale.SelectPreset(600f, float.NaN, 1.3f),
                Is.EqualTo(HDRRangeReduction.ACES1000Nits)
            );
            Assert.That(
                AcesToneScale.SceneValueForNits(float.NaN, 200f, HDRRangeReduction.ACES1000Nits),
                Is.Zero
            );
            Assert.That(
                AcesToneScale.SceneColorForPeakNits(
                    Color.black,
                    600f,
                    200f,
                    HDRRangeReduction.ACES1000Nits
                ),
                Is.EqualTo(Color.black)
            );
        }

        [Test]
        public void DegenerateDisplayPrimariesConvergeWithoutNaNs()
        {
            Vector3 scene = AcesToneScale.SceneColorForDisplayNits(
                new Vector3(0f, 0.00001f, 10f),
                0f,
                HDRRangeReduction.ACES1000Nits
            );

            Assert.That(float.IsFinite(scene.x), Is.True);
            Assert.That(float.IsFinite(scene.y), Is.True);
            Assert.That(float.IsFinite(scene.z), Is.True);
        }

        [Test]
        public void AcesPrivateBoundaryBranchesMatchTheReferenceCurve()
        {
            Assert.That(InvokeFloat("ToneScale", 100000f), Is.EqualTo(10000f));
            Assert.That(InvokeFloat("CenterHue", -200f, 0f), Is.EqualTo(160f));
            Assert.That(InvokeFloat("LinearToAcesCc", 0f), Is.LessThan(0f));
            Assert.That(InvokeFloat("LinearToAcesCc", 0.00001f), Is.LessThan(0f));
            Assert.That(InvokeFloat("AcesCcToLinear", -1f), Is.LessThan(0f));
            Assert.That(
                AcesToneScale.ApplyContrast(new Vector3(0.25f, 0.5f, 1f), 1f),
                Is.EqualTo(new Vector3(0.25f, 0.5f, 1f))
            );
        }

        [Test]
        public void DungeonLuminanceBudgetExposesTheWholeAuthoredLadder()
        {
            SceneLuminanceBudget budget = SceneLuminanceBudget.Dungeon;

            Assert.That(budget.RecessNits, Is.EqualTo(0.05f));
            Assert.That(budget.DistantSurfaceNits, Is.EqualTo(1.75f));
            Assert.That(budget.ShadowSideNits, Is.EqualTo(6f));
            Assert.That(budget.FlamePeakNits, Is.EqualTo(800f));
            Assert.That(SceneLuminanceBudget.NitsToSceneLinear(-1f, 0f), Is.Zero);
            Assert.That(SceneLuminanceBudget.NitsToSceneLinear(-1f, 200f), Is.Zero);
            Assert.That(SceneLuminanceBudget.SceneLinearToNits(-1f, -1f), Is.Zero);
            Assert.That(SceneLuminanceBudget.Ev100For(0f), Is.LessThan(0f));
        }

        [Test]
        public void BareAndBlankSceneNamesRemainStable()
        {
            Assert.That(RenderingSceneNames.LevelOf(null), Is.Null);
            Assert.That(RenderingSceneNames.LevelOf("Dungeon"), Is.EqualTo("Dungeon"));
            Assert.That(RenderingSceneNames.CommonSceneFor(null), Is.Null);
        }

        private static float InvokeFloat(string name, params object[] arguments) =>
            (float)typeof(AcesToneScale).GetMethod(name, StaticPrivate).Invoke(null, arguments);
    }
}
