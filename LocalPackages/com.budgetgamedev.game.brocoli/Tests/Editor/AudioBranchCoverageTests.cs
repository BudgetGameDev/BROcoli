using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class SmallRuntimeBranchCoverageTests
    {
        [Test]
        public void InstanceAudioClippersCoverBothSaturationLimits()
        {
            GameObject host = new("Coverage Audio Clippers");
            try
            {
                foreach (
                    Component component in new Component[]
                    {
                        host.AddComponent<ProceduralFootstepAudio>(),
                        host.AddComponent<ProceduralEnemyWalkAudio>(),
                    }
                )
                {
                    MethodInfo softClip = component
                        .GetType()
                        .GetMethod("SoftClip", BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.That(
                        (float)softClip.Invoke(component, new object[] { 2f }),
                        Is.EqualTo(1f)
                    );
                    Assert.That(
                        (float)softClip.Invoke(component, new object[] { -2f }),
                        Is.EqualTo(-1f)
                    );
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MissingEnemyDeathClipWarnsOnlyOnceAndSkipsPlayback()
        {
            typeof(EnemyDeathAudio)
                .GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
            LogAssert.Expect(
                LogType.Warning,
                "Enemy death SFX is missing at Resources/Brocoli/Audio/SFX/EnemyDeathSplat."
            );
            EnemyDeathAudio.Play(Vector3.zero, false, () => null);
            EnemyDeathAudio.Play(Vector3.zero, false, () => null);
        }
    }
}
