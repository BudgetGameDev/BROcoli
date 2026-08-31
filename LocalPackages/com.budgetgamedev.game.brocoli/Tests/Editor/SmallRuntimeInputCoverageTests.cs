using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class SmallRuntimeBranchCoverageTests
    {
        [Test]
        public void GeneratedTouchWrapperCoversNonGenericEnumerationCallbacksAndFinalizer()
        {
            var action = new TouchAction();
            var callbacks = new TouchCallbacks();
            action.Touch.AddCallbacks(callbacks);
            action.Touch.RemoveCallbacks(callbacks);
            IEnumerator enumerator = ((IEnumerable)action).GetEnumerator();
            Assert.That(enumerator, Is.Not.Null);
            action.Disable();
            typeof(TouchAction)
                .GetMethod("Finalize", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(action, null);
            UnityEngine.Object.DestroyImmediate(action.asset);
        }

        [Test]
        public void GraphicRegistryReflectionFailureIsContained()
        {
            GameObject host = new("Coverage Registry Failure");
            try
            {
                GraphicRegistryCleaner cleaner = host.AddComponent<GraphicRegistryCleaner>();
                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("^\\[GraphicRegistryCleaner\\] Reflection cleanup failed:")
                );
                typeof(GraphicRegistryCleaner)
                    .GetMethod(
                        "CleanupGraphicRegistryType",
                        BindingFlags.Static | BindingFlags.NonPublic
                    )
                    .Invoke(null, new object[] { typeof(ThrowingRegistry) });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private sealed class ThrowingRegistry
        {
            public static object instance => throw new Exception("Coverage registry failure");
        }

        private sealed class TouchCallbacks : TouchAction.ITouchActions
        {
            public void OnPrimaryContact(InputAction.CallbackContext context) { }

            public void OnPrimaryPosition(InputAction.CallbackContext context) { }

            public void OnUP(InputAction.CallbackContext context) { }

            public void OnDOWN(InputAction.CallbackContext context) { }

            public void OnLEFT(InputAction.CallbackContext context) { }

            public void OnRIGHT(InputAction.CallbackContext context) { }
        }
    }
}
