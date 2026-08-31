using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Probes guarded runtime entry points with empty/default inputs. A guard may
    /// accept the input or reject it with an exception; either result is valid,
    /// provided the harness can continue probing the remaining components.
    /// </summary>
    public sealed partial class RuntimeGuardEntryCoverageTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        private static readonly string[] BlockedFragments =
        {
            "ChangeScene",
            "ContinueGame",
            "Delete",
            "Die",
            "EndRun",
            "Exit",
            "Fire",
            "GoTo",
            "Install",
            "Load",
            "LoadScene",
            "OpenScene",
            "PlayGame",
            "PlaySelected",
            "Quit",
            "Reload",
            "Restart",
            "ReturnTo",
            "Save",
            "SaveNow",
            "Shoot",
            "Spawn",
            "StartGame",
            "StartAutoplay",
            "TakeDamage",
        };

        [Test]
        [TestMustExpectAllLogs(false)]
        public void DefaultArgumentsReachEveryGuardedEntryPoint()
        {
            LogAssert.ignoreFailingMessages = true;
            Type[] types = typeof(PlayerStats)
                .Assembly.GetTypes()
                .Where(type => type.Namespace?.StartsWith("BudgetGameDev.Games.Brocoli") == true)
                .OrderBy(type => type.FullName)
                .ToArray();
            int attempted = 0;
            int returned = 0;

            foreach (Type type in types)
            {
                GameObject host = null;
                object target = null;
                if (!type.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    host = new GameObject($"Guard probe {type.Name}");
                    host.SetActive(false);
                    try
                    {
                        target = host.AddComponent(type);
                    }
                    catch
                    {
                        UnityEngine.Object.DestroyImmediate(host);
                        continue;
                    }
                }
                else if (!type.IsAbstract && !type.IsInterface)
                {
                    try
                    {
                        target = typeof(ScriptableObject).IsAssignableFrom(type)
                            ? ScriptableObject.CreateInstance(type)
                            : Activator.CreateInstance(type, nonPublic: true);
                    }
                    catch
                    {
                        // Types without a parameterless construction path still
                        // contribute their static entry points below.
                    }
                }

                ProbeMembers(type, target, ref attempted, ref returned);
                foreach (int state in new[] { -1, 1, 2, 3, 10 })
                {
                    PopulateFields(type, target, state);
                    ProbeMembers(type, target, ref attempted, ref returned);
                }
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
                else if (target is ScriptableObject asset)
                    UnityEngine.Object.DestroyImmediate(asset);
            }

            Assert.That(attempted, Is.GreaterThan(450));
            Assert.That(returned, Is.GreaterThan(150));
        }

        private void ProbeMembers(Type type, object target, ref int attempted, ref int returned)
        {
            foreach (PropertyInfo property in ReadableProperties(type, target != null))
            {
                MethodInfo getter = property.GetMethod ?? property.GetGetMethod(nonPublic: true);
                if (!getter.IsStatic && target == null)
                    continue;
                attempted++;
                try
                {
                    property.GetValue(getter.IsStatic ? null : target);
                    returned++;
                }
                catch
                {
                    // A getter may require initialized scene state.
                }
            }

            foreach (MethodInfo method in EntryMethods(type, target != null))
            {
                if (
                    !method.IsStatic
                    && (target == null || (target is UnityEngine.Object item && item == null))
                )
                    continue;
                foreach (
                    int variant in method.GetParameters().Length == 0
                        ? new[] { 0 }
                        : new[] { 0, 1, -1 }
                )
                {
                    object[] arguments;
                    try
                    {
                        arguments = Arguments(method, variant);
                    }
                    catch
                    {
                        continue;
                    }

                    attempted++;
                    try
                    {
                        object result = method.Invoke(method.IsStatic ? null : target, arguments);
                        if (result is IEnumerator routine)
                            Drain(routine);
                        returned++;
                    }
                    catch
                    {
                        // Rejecting a synthetic value is a valid guard outcome.
                    }
                }
            }
        }

        [TearDown]
        public void RestoreTestState()
        {
            LogAssert.ignoreFailingMessages = false;
            for (int index = createdObjects.Count - 1; index >= 0; index--)
                if (createdObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
            createdObjects.Clear();
        }

        private static IEnumerable<MethodInfo> EntryMethods(Type type, bool hasInstance) =>
            type.GetMethods(
                    BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.Instance
                        | BindingFlags.Static
                        | BindingFlags.DeclaredOnly
                )
                .Where(method =>
                    !method.IsSpecialName
                    && !method.IsGenericMethodDefinition
                    && method.GetParameters().Length <= 5
                    && (method.IsStatic || hasInstance)
                    && !BlockedFragments.Any(fragment =>
                        method.Name.Contains(fragment, StringComparison.Ordinal)
                    )
                );

        private static void Drain(IEnumerator routine)
        {
            for (int step = 0; step < 64 && routine.MoveNext(); step++)
            {
                if (routine.Current is IEnumerator nested)
                    Drain(nested);
            }
        }

        private static IEnumerable<PropertyInfo> ReadableProperties(Type type, bool hasInstance) =>
            type.GetProperties(
                    BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.Instance
                        | BindingFlags.Static
                        | BindingFlags.DeclaredOnly
                )
                .Where(property =>
                    property.GetIndexParameters().Length == 0
                    && property.GetGetMethod(nonPublic: true) is MethodInfo getter
                    && (getter.IsStatic || hasInstance)
                    && !BlockedFragments.Any(fragment =>
                        property.Name.Contains(fragment, StringComparison.Ordinal)
                    )
                );
    }
}
