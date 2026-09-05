using BudgetGameDev.Shared.Rendering.Universal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using HD = UnityEngine.Rendering.HighDefinition;
using URP = UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Rendering.HighDefinition.Tests
{
    public sealed class SdrHighlightGradeTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SwitchingOutputKeepsSdrHighlightsOutOfHdr(bool highDefinition)
        {
            GameObject host = new("Output grade test");
            IHdrGradeFrontEnd grade = highDefinition
                ? new HighDefinitionHdrGradeFrontEnd()
                : new UniversalHdrGradeFrontEnd();
            var deferred = new System.Collections.Generic.List<Object>();
            try
            {
                grade.Attach(host);
                var volume = host.GetComponent<Volume>();
                foreach (bool hdr in new[] { false, true, false, true })
                {
                    grade.Apply(
                        new HdrGradeRequest(
                            hdr,
                            HDRRangeReduction.ACES1000Nits,
                            200f,
                            0.0005f,
                            600f,
                            false,
                            12f,
                            17f,
                            -0.0008f
                        )
                    );
                    Assert.That(volume.enabled, Is.True);
                    if (highDefinition)
                    {
                        volume.profile.TryGet(out HD.ShadowsMidtonesHighlights highlights);
                        volume.profile.TryGet(out ImpressionistBloom bloom);
                        volume.profile.TryGet(out HD.Tonemapping tone);
                        volume.profile.TryGet(out HD.ColorAdjustments color);
                        Assert.That(highlights.active, Is.EqualTo(!hdr));
                        Assert.That(bloom.active, Is.EqualTo(!hdr));
                        Assert.That(tone.active, Is.EqualTo(hdr));
                        Assert.That(color.active, Is.EqualTo(hdr));
                        Assert.That(color.postExposure.overrideState, Is.False);
                        CheckHighlightGain(highlights.highlights.value, 1.2f);
                        Assert.That(bloom.intensity.overrideState, Is.False);
                        Assert.That(bloom.threshold.value, Is.EqualTo(1f));
                        Assert.That(bloom.scatter.value, Is.EqualTo(0.62f));
                    }
                    else
                    {
                        volume.profile.TryGet(out URP.ShadowsMidtonesHighlights highlights);
                        volume.profile.TryGet(out URP.Bloom bloom);
                        volume.profile.TryGet(out URP.Tonemapping tone);
                        volume.profile.TryGet(out URP.ColorAdjustments color);
                        Assert.That(highlights.active, Is.EqualTo(!hdr));
                        Assert.That(bloom.active, Is.EqualTo(!hdr));
                        Assert.That(tone.active, Is.EqualTo(hdr));
                        Assert.That(color.active, Is.EqualTo(hdr));
                        Assert.That(color.postExposure.overrideState, Is.False);
                        CheckHighlightGain(highlights.highlights.value, 1.35f);
                        Assert.That(bloom.intensity.overrideState, Is.False);
                        Assert.That(bloom.threshold.value, Is.EqualTo(1f));
                        Assert.That(bloom.scatter.value, Is.EqualTo(0.62f));
                    }
                }
                // A pipeline change must immediately remove the old grade, even with deferred destruction.
                grade.Detach(true, deferred.Add, Object.DestroyImmediate);
                Assert.That(volume.enabled, Is.False);
                Assert.That(volume.sharedProfile, Is.Null);
            }
            finally
            {
                grade.Detach(false, Object.DestroyImmediate, Object.DestroyImmediate);
                foreach (Object obj in deferred)
                    Object.DestroyImmediate(obj);
                Object.DestroyImmediate(host);
            }
        }

        private static void CheckHighlightGain(Vector4 wheel, float expected)
        {
            var (shadows, midtones, highlights) = ColorUtils.PrepareShadowsMidtonesHighlights(
                new Vector4(1f, 1f, 1f, 0f),
                new Vector4(1f, 1f, 1f, 0f),
                wheel
            );
            Assert.That(shadows.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(midtones.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(highlights.x, Is.EqualTo(expected).Within(0.0001f));
        }
    }
}
