using System;
using System.Reflection;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class SmallRuntimeBranchCoverageTests
    {
        [Test]
        [TestMustExpectAllLogs(false)]
        public void PwaFullscreenExitAndTogglePathsAreCallableInEditor()
        {
            PWAHelper.LeaveFullscreen();
            int enters = 0;
            int leaves = 0;
            MethodInfo toggle = typeof(PWAHelper).GetMethod(
                "ToggleFullscreen",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(bool), typeof(Action), typeof(Action) },
                null
            );
            toggle.Invoke(
                null,
                new object[] { false, (Action)(() => enters++), (Action)(() => leaves++) }
            );
            toggle.Invoke(
                null,
                new object[] { true, (Action)(() => enters++), (Action)(() => leaves++) }
            );
            Assert.That(enters, Is.EqualTo(1));
            Assert.That(leaves, Is.EqualTo(1));
        }
    }
}
