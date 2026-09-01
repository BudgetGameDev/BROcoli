using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public class HdrLightBalanceTests
    {
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
