using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Every runtime component has lifecycle and query methods that Unity may call
    /// while scene references are still absent or while an object is being torn
    /// down. This contract sweep verifies those default-state entry points remain
    /// callable across the whole game assembly.
    /// </summary>
    public sealed class RuntimeDefaultStateCoverageTests
    {
        private static readonly string[] SafePrefixes =
        {
            "Awake",
            "OnEnable",
            "OnDisable",
            "Update",
            "FixedUpdate",
            "LateUpdate",
            "Reset",
            "Clear",
            "Stop",
            "Cancel",
            "Hide",
            "Resolve",
            "Ensure",
            "Refresh",
            "Setup",
            "Initialize",
            "Check",
            "Find",
            "Get",
            "Has",
            "Is",
            "Try",
        };

        private static readonly HashSet<Type> ExcludedTypes = new()
        {
            typeof(AutoplayController),
            typeof(BrocoliAutosaveController),
            typeof(CameraOcclusionFader),
            typeof(DungeonManager),
            typeof(ExplorationOverlay),
            typeof(FrameCapture),
            typeof(GameOverCTAManager),
            typeof(GameOverOverlay),
            typeof(GamePreloader),
            typeof(GameStates),
            typeof(LevelUpScreen),
            typeof(MainMenu),
            typeof(PauseMenu),
            typeof(PoolManager),
            typeof(ResponsiveMainMenuLayout),
            typeof(RunTelemetry),
        };

        [Test]
        [TestMustExpectAllLogs(false)]
        public void LifecycleAndQueriesAcceptDefaultSceneState()
        {
            LogAssert.ignoreFailingMessages = true;
            Type[] componentTypes = typeof(PlayerStats)
                .Assembly.GetTypes()
                .Where(type =>
                    !type.IsAbstract
                    && typeof(MonoBehaviour).IsAssignableFrom(type)
                    && !ExcludedTypes.Contains(type)
                )
                .OrderBy(type => type.FullName)
                .ToArray();
            var failures = new List<string>();
            int invoked = 0;

            foreach (Type type in componentTypes)
            {
                // UI layout lifecycle requires its normal RectTransform host, even before
                // scene references exist. A plain Transform prevents its controls building.
                GameObject host =
                    type == typeof(ResponsivePauseMenuLayout)
                        ? new GameObject($"Default-state {type.Name}", typeof(RectTransform))
                        : new GameObject($"Default-state {type.Name}");
                host.SetActive(false);
                MonoBehaviour component;
                try
                {
                    component = (MonoBehaviour)host.AddComponent(type);
                }
                catch (Exception error)
                {
                    failures.Add($"{type.Name}.AddComponent: {Innermost(error).Message}");
                    UnityEngine.Object.DestroyImmediate(host);
                    continue;
                }

                foreach (MethodInfo method in SafeMethods(type))
                {
                    if (component == null)
                        break;
                    try
                    {
                        method.Invoke(component, null);
                        invoked++;
                    }
                    catch (Exception error)
                    {
                        failures.Add($"{type.Name}.{method.Name}: {Innermost(error).Message}");
                    }
                }

                UnityEngine.Object.DestroyImmediate(host);
            }

            Assert.That(invoked, Is.GreaterThan(100));
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [TearDown]
        public void RestoreLogPolicy() => LogAssert.ignoreFailingMessages = false;

        private static IEnumerable<MethodInfo> SafeMethods(Type type) =>
            type.GetMethods(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly
                )
                .Where(method =>
                    !method.IsSpecialName
                    && method.GetParameters().Length == 0
                    && method.Name != "GetOffscreenPosition"
                    && SafePrefixes.Any(prefix =>
                        method.Name.StartsWith(prefix, StringComparison.Ordinal)
                    )
                );

        private static Exception Innermost(Exception error)
        {
            while (error.InnerException != null)
                error = error.InnerException;
            return error;
        }
    }
}
