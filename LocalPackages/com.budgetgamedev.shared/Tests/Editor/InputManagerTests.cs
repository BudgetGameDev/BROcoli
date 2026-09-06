using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// The touch/keyboard front end. Awake and Start never run by themselves in an
    /// editor test, so the fixture drives the component's lifecycle by hand.
    /// </summary>
    public sealed class InputManagerTests
    {
        private GameObject cameraObject;
        private GameObject host;
        private InputManager manager;

        [SetUp]
        public void CreateManager()
        {
            foreach (Camera camera in Object.FindObjectsByType<Camera>())
            {
                if (camera.CompareTag("MainCamera"))
                    Object.DestroyImmediate(camera.gameObject);
            }

            cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            host = new GameObject("InputManager");
            manager = host.AddComponent<InputManager>();
            manager.Awake();
        }

        [TearDown]
        public void DestroyManager()
        {
            manager.OnDisable();

            // TouchAction.Dispose destroys its asset with Object.Destroy, which the
            // editor refuses outside play mode, so the asset is released directly.
            if (manager.touchAction != null)
            {
                Object.DestroyImmediate(manager.touchAction.asset);
            }

            Object.DestroyImmediate(host);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void AwakeBuildsTheTouchActionsAndBindsToTheSceneMainCamera()
        {
            Assert.That(manager.touchAction, Is.Not.Null, "the action asset is built in Awake");
            Assert.That(
                manager.mainCamera,
                Is.SameAs(cameraObject.GetComponent<Camera>()),
                "touch points are projected through the scene's main camera"
            );
        }

        [Test]
        public void EnablingTheComponentEnablesTheTouchActions()
        {
            manager.OnEnable();

            Assert.That(manager.touchAction.Touch.PrimaryContact.enabled, Is.True);
            Assert.That(manager.touchAction.Touch.UP.enabled, Is.True);

            manager.OnDisable();

            Assert.That(manager.touchAction.Touch.PrimaryContact.enabled, Is.False);
            Assert.That(manager.touchAction.Touch.UP.enabled, Is.False);
        }

        [Test]
        public void AComponentThatNeverAwokeStillEnablesAndDisablesCleanly()
        {
            var bare = new GameObject("Bare").AddComponent<InputManager>();

            Assert.DoesNotThrow(() => bare.OnEnable());
            Assert.DoesNotThrow(() => bare.OnDisable());

            Object.DestroyImmediate(bare.gameObject);
        }

        [Test]
        public void TheTouchCallbacksReportAWorldPointAndTheEventsOwnTime()
        {
            Vector2 startPosition = Vector2.positiveInfinity;
            Vector2 endPosition = Vector2.positiveInfinity;
            float startTime = -1f;
            float endTime = -1f;
            manager.OnStartTouch += (position, time) =>
            {
                startPosition = position;
                startTime = time;
            };
            manager.OnEndTouch += (position, time) =>
            {
                endPosition = position;
                endTime = time;
            };

            manager.StartTouchPrimary(default);
            manager.EndTouchPrimary(default);

            Assert.That(startPosition, Is.EqualTo(manager.PrimaryPosition()));
            Assert.That(endPosition, Is.EqualTo(manager.PrimaryPosition()));
            Assert.That(startTime, Is.EqualTo(0f), "an unbound context carries no timestamp");
            Assert.That(endTime, Is.EqualTo(0f));
        }

        [Test]
        public void EveryDirectionCallbackReachesItsOwnListener()
        {
            int up = 0;
            int down = 0;
            int left = 0;
            int right = 0;
            manager.OnUP += axis => up++;
            manager.OnDOWN += axis => down++;
            manager.OnLEFT += axis => left++;
            manager.OnRIGHT += axis => right++;

            manager.UPPrimary(default);
            manager.DOWNPrimary(default);
            manager.LEFTPrimary(default);
            manager.RIGHTPrimary(default);

            Assert.That(new[] { up, down, left, right }, Is.EqualTo(new[] { 1, 1, 1, 1 }));
        }

        [Test]
        public void ASwipeReachesItsListener()
        {
            Vector2 swipe = Vector2.zero;
            manager.OnSwipeDirection += direction => swipe = direction;

            manager.TriggerSwipeDirection(Vector2.left);

            Assert.That(swipe, Is.EqualTo(Vector2.left));
        }

        [Test]
        public void CallbacksWithNothingListeningAreHarmless()
        {
            Assert.DoesNotThrow(() =>
            {
                manager.StartTouchPrimary(default);
                manager.EndTouchPrimary(default);
                manager.UPPrimary(default);
                manager.DOWNPrimary(default);
                manager.LEFTPrimary(default);
                manager.RIGHTPrimary(default);
                manager.TriggerSwipeDirection(Vector2.up);
            });
        }

        [Test]
        public void StartWiresTheActionsAndSettlesTheOnScreenController()
        {
            // Touch hardware and the Device Simulator can make an Editor mobile.
            // Supply the controller Start owns, then assert the actual platform decision.
            bool isMobile = InputManager.IncludeEditorMobileSimulation(
                InputManager.IsMobileDevice(
                    Application.platform,
                    SystemInfo.deviceType,
                    Input.touchSupported
                ),
                UnityEngine.Device.SystemInfo.deviceType,
                Input.touchSupported
            );
            var controllerHost = new GameObject("Start fixture virtual controller");
            controllerHost.transform.SetParent(host.transform);
            controllerHost.SetActive(false);
            controllerHost.AddComponent<VirtualController>();
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape("[InputManager] Platform:")));
            if (isMobile)
                LogAssert.Expect(
                    LogType.Log,
                    "[InputManager] VirtualController found and activated. Active: True"
                );

            manager.Start();
            Assert.That(controllerHost.activeSelf, Is.EqualTo(isMobile));

            int started = 0;
            manager.OnUP += axis => started++;
            manager.UPPrimary(default);

            Assert.That(started, Is.EqualTo(1), "Start must leave the callbacks usable");
        }
    }
}
