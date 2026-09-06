using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BudgetGameDev.Shared.Tests
{
    public sealed partial class VirtualControllerTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject root;
        private VirtualController controller;
        private RectTransform joystick;
        private RectTransform handle;
        private Button action;
        private Button pause;
        private Canvas canvas;

        [SetUp]
        public void CreateController()
        {
            PlayerPrefs.DeleteKey("ShowVirtualController");
            if (EnhancedTouchSupport.enabled)
                EnhancedTouchSupport.Disable();
            foreach (EventSystem events in Object.FindObjectsByType<EventSystem>())
                Object.DestroyImmediate(events.gameObject);

            root = new GameObject("Virtual Controller");
            controller = root.AddComponent<VirtualController>();

            GameObject canvasObject = new("Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(root.transform);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);

            joystick = CreateImage("Joystick", canvasRect, new Vector2(164f, 164f));
            handle = CreateImage("Handle", joystick, new Vector2(64f, 64f));
            action = CreateButton("Action", canvasRect);
            pause = CreateButton("Pause", canvasRect);

            Set("joystickBackground", joystick);
            Set("joystickHandle", handle);
            Set("actionButton", action);
            Set("pauseButton", pause);
            Set("canvas", canvas);
        }

        [TearDown]
        public void DestroyController()
        {
            if (controller != null)
                Invoke("OnDestroy");
            if (EnhancedTouchSupport.enabled)
                EnhancedTouchSupport.Disable();
            PlayerPrefs.DeleteKey("ShowVirtualController");
            Object.DestroyImmediate(root);
        }

        [Test]
        public void MobilePreferenceBuildsTheControlsAndProcessesJoystickMovement()
        {
            PlayerPrefs.SetInt("ShowVirtualController", 1);
            Set("isMobileCacheSet", false);
            LogAssert.Expect(
                LogType.Log,
                "[VirtualController] User selected 'Play on Mobile' - showing virtual controller"
            );
            LogAssert.Expect(
                LogType.Log,
                new System.Text.RegularExpressions.Regex("^\\[VirtualController\\] Awake -")
            );
            LogAssert.Expect(LogType.Log, "[VirtualController] EnhancedTouchSupport enabled");
            LogAssert.Expect(LogType.Log, "[VirtualController] Visible and ready");
            Invoke("Awake");

            LogAssert.Expect(
                LogType.Warning,
                "[VirtualController] No IPauseController in scene - pause button won't work"
            );
            Invoke("Start");

            Assert.That(joystick.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(handle.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(pause.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(action.gameObject.activeSelf, Is.False);

            Invoke(
                "UpdateJoystickPosition",
                RectTransformUtility.WorldToScreenPoint(null, joystick.position)
            );
            Assert.That(controller.JoystickInput, Is.EqualTo(Vector2.zero));

            Invoke("UpdateJoystickPosition", new Vector2(Screen.width, Screen.height));
            Assert.That(controller.JoystickInput.magnitude, Is.GreaterThan(0.2f));
            Invoke("ResetJoystick");
            Assert.That(controller.JoystickInput, Is.EqualTo(Vector2.zero));
            Assert.That(handle.anchoredPosition, Is.EqualTo(Vector2.zero));

            Invoke("OnDisable");
            Assert.That(EnhancedTouchSupport.enabled, Is.False);
            Invoke("OnEnable");
            Assert.That(EnhancedTouchSupport.enabled, Is.True);
        }

        [Test]
        public void DesktopPreferenceHidesTouchControlsButKeepsPauseAvailable()
        {
            PlayerPrefs.SetInt("ShowVirtualController", 0);
            LogAssert.Expect(
                LogType.Log,
                "[VirtualController] User selected 'Play' - hiding virtual controller"
            );
            LogAssert.Expect(
                LogType.Log,
                new System.Text.RegularExpressions.Regex("^\\[VirtualController\\] Awake -")
            );
            LogAssert.Expect(
                LogType.Log,
                "[VirtualController] Hiding joystick controls, keeping pause button"
            );

            Invoke("Awake");

            Assert.That(joystick.gameObject.activeSelf, Is.False);
            Assert.That(action.gameObject.activeSelf, Is.False);
            Assert.That(pause.gameObject.activeSelf, Is.True);
            Assert.That(VirtualController.Instance, Is.SameAs(controller));
        }

        [Test]
        public void ActionButtonSubmitsToTheSelectedControlButNotToItself()
        {
            GameObject eventObject = new("Event System", typeof(EventSystem));
            Button target = CreateButton("Submit Target", root.transform);
            try
            {
                EventSystem events = eventObject.GetComponent<EventSystem>();
                typeof(EventSystem)
                    .GetMethod("OnEnable", Members)
                    .Invoke(events, System.Array.Empty<object>());
                EventSystem.current = events;
                Assert.That(EventSystem.current, Is.SameAs(events));
                events.SetSelectedGameObject(action.gameObject);
                Invoke("OnActionButtonPressed");

                int submits = 0;
                target.onClick.AddListener(() => submits++);
                events.SetSelectedGameObject(target.gameObject);
                Invoke("OnActionButtonPressed");

                Assert.That(submits, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(target.gameObject);
                Object.DestroyImmediate(eventObject);
            }
        }

        [Test]
        public void TextureAndSafeAreaHelpersHandleEdgesAndMissingLayout()
        {
            Texture2D ring = Invoke<Texture2D>("CreateRingTexture", 16, 3f, Color.red, Color.blue);
            Texture2D circle = Invoke<Texture2D>(
                "CreateCircleTexture",
                16,
                Color.green,
                Color.white
            );
            Texture2D icon = Invoke<Texture2D>(
                "CreatePauseIconTexture",
                16,
                Color.black,
                Color.white
            );
            try
            {
                Assert.That(ring.GetPixels(), Has.Some.Not.EqualTo(Color.clear));
                Assert.That(circle.GetPixels(), Has.Some.Not.EqualTo(Color.clear));
                Assert.That(icon.GetPixels(), Has.Some.Not.EqualTo(Color.clear));
                Assert.That(
                    Invoke<Vector2>("ClampAnchorToSafeArea", null, new Vector2(0.2f, 0.8f)),
                    Is.EqualTo(new Vector2(0.2f, 0.8f))
                );
                Set("pauseButton", null);
                Invoke("SetupPauseButtonVisual");
                Set("pauseButton", pause);
                GameObject legacyLabel = new("Legacy Label", typeof(RectTransform), typeof(Text));
                legacyLabel.transform.SetParent(pause.transform, false);
                Invoke("SetupPauseButtonVisual");
                Assert.That(legacyLabel.activeSelf, Is.False);
                RectTransform canvasRect = canvas.transform as RectTransform;
                Vector2 canvasSize = canvasRect.sizeDelta;
                canvasRect.sizeDelta = Vector2.zero;
                Invoke("ClampAnchorToSafeArea", joystick, Vector2.one * 0.5f);
                canvasRect.sizeDelta = canvasSize;
                Set("joystickBackground", null);
                Invoke("HandleJoystickInput");
            }
            finally
            {
                Object.DestroyImmediate(ring);
                Object.DestroyImmediate(circle);
                Object.DestroyImmediate(icon);
            }
        }

        [Test]
        public void EnhancedTouchInputIsForwardedToTheJoystickProcessor()
        {
            Touchscreen screen = InputSystem.AddDevice<Touchscreen>();
            try
            {
                EnhancedTouchSupport.Enable();
                Canvas.ForceUpdateCanvases();
                Vector2 touchPosition = RectTransformUtility.WorldToScreenPoint(
                    null,
                    joystick.TransformPoint(new Vector3(30f, 0f, 0f))
                );
                InputSystem.QueueStateEvent(
                    screen,
                    new TouchState
                    {
                        touchId = 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Began,
                        position = touchPosition,
                    }
                );
                InputSystem.Update();
                Assert.That(
                    UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count,
                    Is.EqualTo(1)
                );
                LogAssert.Expect(
                    LogType.Log,
                    "[VirtualController] Touch began on joystick, finger: 0"
                );
                Invoke("HandleJoystickInput");
                Assert.That(controller.JoystickInput.x, Is.GreaterThan(0.1f));
            }
            finally
            {
                InputSystem.RemoveDevice(screen);
            }
        }

        private static RectTransform CreateImage(string name, Transform parent, Vector2 size)
        {
            GameObject child = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            child.transform.SetParent(parent, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return rect;
        }

        private static Button CreateButton(string name, Transform parent)
        {
            GameObject child = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            child.transform.SetParent(parent, false);
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 64f);
            return child.GetComponent<Button>();
        }

        private void Set(string field, object value) =>
            controller.GetType().GetField(field, Members).SetValue(controller, value);

        private object Invoke(string method, params object[] arguments) =>
            controller.GetType().GetMethod(method, Members).Invoke(controller, arguments);

        private T Invoke<T>(string method, params object[] arguments) =>
            (T)Invoke(method, arguments);
    }
}
