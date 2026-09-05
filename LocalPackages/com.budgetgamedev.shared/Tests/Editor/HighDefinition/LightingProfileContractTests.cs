using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;
using HD = UnityEngine.Rendering.HighDefinition;

namespace BudgetGameDev.Shared.Rendering.HighDefinition.Tests
{
    public sealed class LightingProfileContractTests
    {
        private static readonly string[] RenderingProfiles =
        {
            "Assets/Settings/Rendering/HDRP/BROcoli HDRP Medium Volume.asset",
            "Assets/Settings/Rendering/HDRP/BROcoli HDRP High Volume.asset",
            "Assets/Settings/Rendering/HDRP/BROcoli HDRP RT High Volume.asset",
            "Assets/Settings/Rendering/HDRP/BROcoli HDRP RT Ultra Volume.asset",
            "Packages/com.budgetgamedev.game.brocoli/Rendering/Brocoli_Dungeon_HDRP Volume Profile.asset",
            "Packages/com.budgetgamedev.game.brocoli/Rendering/Brocoli_MainMenu_HDRP Volume Profile.asset",
        };

        [Test]
        public void AllRenderingProfilesKeepPhysicalExposureAndTheAuthoredAdditiveBloom()
        {
            VolumeProfile defaults = Load(
                "Assets/HDRPDefaultResources/DefaultSettingsVolumeProfile.asset"
            );
            CheckExposure(defaults);
            CheckNativeBloomDisabled(defaults);
            foreach (string path in RenderingProfiles)
            {
                VolumeProfile profile = Load(path);
                CheckExposure(profile);
                // Quality tiers inherit bloom from the additive game rendering scene.
                if (!path.StartsWith("Packages/"))
                    continue;

                Assert.That(profile.TryGet(out ImpressionistBloom additive), Is.True, path);
                Assert.That(additive.active, Is.True, path);
                Assert.That(additive.intensity.overrideState, Is.True, path);
                Assert.That(additive.intensity.value, Is.EqualTo(1.35f).Within(0.0001f), path);
                Assert.That(additive.threshold.overrideState, Is.True, path);
                Assert.That(additive.threshold.value, Is.EqualTo(0.85f).Within(0.0001f), path);
                Assert.That(additive.scatter.overrideState, Is.True, path);
                Assert.That(additive.scatter.value, Is.EqualTo(0.72f).Within(0.0001f), path);
                CheckNativeBloomDisabled(profile);
            }
        }

        private static void CheckNativeBloomDisabled(VolumeProfile profile)
        {
            Assert.That(profile.TryGet(out HD.Bloom native), Is.True, profile.name);
            Assert.That(
                native.active,
                Is.True,
                "Suppress inherited veiling glare: " + profile.name
            );
            Assert.That(native.intensity.overrideState, Is.True, profile.name);
            Assert.That(native.intensity.value, Is.Zero, profile.name);
        }

        private static VolumeProfile Load(string path)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assert.That(profile, Is.Not.Null, path);
            return profile;
        }

        private static void CheckExposure(VolumeProfile profile)
        {
            Assert.That(profile.TryGet(out HD.Exposure exposure), Is.True, profile.name);
            Assert.That(exposure.active, Is.True, profile.name);
            Assert.That(exposure.mode.overrideState, Is.True, profile.name);
            Assert.That(exposure.mode.value, Is.EqualTo(HD.ExposureMode.Fixed), profile.name);
            Assert.That(exposure.fixedExposure.overrideState, Is.True, profile.name);
            Assert.That(
                exposure.fixedExposure.value,
                Is.EqualTo(SceneLuminanceBudget.Dungeon.FixedExposureEv100).Within(0.0001f),
                profile.name
            );
            Assert.That(exposure.compensation.overrideState, Is.True, profile.name);
            Assert.That(exposure.compensation.value, Is.Zero, profile.name);
        }
    }
}
