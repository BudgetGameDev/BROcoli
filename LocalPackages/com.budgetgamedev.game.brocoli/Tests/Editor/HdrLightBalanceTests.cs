using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public class HdrLightBalanceTests
    {
        [TestCase(false, false, 100f, 60f)]
        [TestCase(true, false, 45f, 60f)]
        [TestCase(false, true, 28f, 14f)]
        [TestCase(true, true, 12.6f, 14f)]
        public void PipelineFillBalanceComposesWithDisplayBalanceAndRestoresAuthoredLight(
            bool hdr,
            bool highDefinition,
            float intensity,
            float range
        )
        {
            GameObject root = new("Fill");
            try
            {
                Light light = root.AddComponent<Light>();
                light.intensity = 100f;
                light.range = 60f;
                var balance = root.AddComponent<HdrLightBalance>();
                balance.Bind(light, 0.45f, 0.28f, 14f / 60f);

                balance.SetBalance(hdr, highDefinition);
                balance.SetBalance(hdr, highDefinition);
                Assert.That(light.intensity, Is.EqualTo(intensity).Within(0.001f));
                Assert.That(light.range, Is.EqualTo(range).Within(0.001f));

                balance.SetBalance(false, false);
                Assert.That(light.intensity, Is.EqualTo(100f).Within(0.001f));
                Assert.That(light.range, Is.EqualTo(60f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HdrScalesTheAuthoredIntensityAndSdrLeavesItAlone()
        {
            GameObject root = new("Fill");
            try
            {
                Light light = root.AddComponent<Light>();
                light.intensity = 85f;
                var balance = root.AddComponent<HdrLightBalance>();
                balance.Bind(light, 0.45f);

                balance.SetHdrBalance(true);
                Assert.That(light.intensity, Is.EqualTo(38.25f).Within(0.01f));

                balance.SetHdrBalance(false);
                Assert.That(light.intensity, Is.EqualTo(85f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RepeatedApplicationDoesNotCompound()
        {
            GameObject root = new("Fill");
            try
            {
                Light light = root.AddComponent<Light>();
                light.intensity = 10f;
                var balance = root.AddComponent<HdrLightBalance>();
                balance.Bind(light, 2f);

                balance.SetHdrBalance(true);
                balance.SetHdrBalance(true);
                balance.SetHdrBalance(true);

                Assert.That(light.intensity, Is.EqualTo(20f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
