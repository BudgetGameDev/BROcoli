using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class TouchActionWrapperTests
    {
        [Test]
        public void AssetFacadeExposesMasksDevicesBindingsAndEnumeration()
        {
            var wrapper = new TouchAction();
            InputDevice gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                var mask = new InputBinding { action = "UP" };
                wrapper.bindingMask = mask;
                Assert.That(wrapper.bindingMask.HasValue, Is.True);
                wrapper.bindingMask = null;
                Assert.That(wrapper.bindingMask.HasValue, Is.False);

                wrapper.devices = new ReadOnlyArray<InputDevice>(new InputDevice[] { gamepad });
                Assert.That(wrapper.devices.Value, Has.Count.EqualTo(1));
                wrapper.devices = null;
                Assert.That(wrapper.devices.HasValue, Is.False);
                Assert.That(wrapper.controlSchemes, Is.Empty);

                Assert.That(wrapper.Contains(wrapper.Touch.UP), Is.True);
                Assert.That(wrapper.FindAction("Touch/UP", true), Is.SameAs(wrapper.Touch.UP));
                Assert.That(
                    wrapper.FindBinding(mask, out InputAction found),
                    Is.GreaterThanOrEqualTo(0)
                );
                Assert.That(found, Is.SameAs(wrapper.Touch.UP));
                Assert.That(wrapper.bindings, Is.Not.Empty);
                Assert.That(wrapper.ToList(), Has.Count.GreaterThan(0));

                IEnumerable nongeneric = wrapper;
                Assert.That(nongeneric.Cast<InputAction>().ToList(), Has.Count.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(wrapper.asset);
                InputSystem.RemoveDevice(gamepad);
            }
        }

        [Test]
        public void TouchMapSupportsEnableConversionAndCallbackReplacement()
        {
            var wrapper = new TouchAction();
            var first = new CallbackSink();
            var second = new CallbackSink();
            try
            {
                TouchAction.TouchActions touch = wrapper.Touch;
                Assert.That(touch.Get(), Is.Not.Null);
                InputActionMap map = touch;
                Assert.That(map, Is.SameAs(touch.Get()));

                touch.Enable();
                Assert.That(touch.enabled, Is.True);
                touch.Disable();
                Assert.That(touch.enabled, Is.False);

                touch.AddCallbacks(null);
                touch.AddCallbacks(first);
                touch.AddCallbacks(first);
                touch.RemoveCallbacks(second);
                touch.SetCallbacks(second);
                touch.SetCallbacks(null);
                touch.RemoveCallbacks(first);
                Assert.Pass();
            }
            finally
            {
                Object.DestroyImmediate(wrapper.asset);
            }
        }

        [Test]
        public void GeneratedDisposeForwardsToUnityObjectDestruction()
        {
            var wrapper = new TouchAction();
            try
            {
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "^Destroy may not be called from edit mode!"
                    )
                );
                Assert.DoesNotThrow(() => wrapper.Dispose());
            }
            finally
            {
                if (wrapper.asset != null)
                    Object.DestroyImmediate(wrapper.asset);
            }
        }

        private sealed class CallbackSink : TouchAction.ITouchActions
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
