using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class LegacyRuntimeCoverageTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void CameraMobileAndAspectCalculationsCoverEveryMode()
        {
            Assert.That(
                CameraController.IsMobileEnvironment(
                    DeviceType.Handheld,
                    false,
                    DeviceType.Desktop
                ),
                Is.True
            );
            Assert.That(
                CameraController.IsMobileEnvironment(DeviceType.Desktop, true, DeviceType.Desktop),
                Is.True
            );
            Assert.That(
                CameraController.IsMobileEnvironment(
                    DeviceType.Desktop,
                    false,
                    DeviceType.Handheld
                ),
                Is.True
            );
            Assert.That(
                CameraController.IsMobileEnvironment(DeviceType.Desktop, false, DeviceType.Desktop),
                Is.False
            );
            Assert.That(
                CameraController.CalculateTargetFov(false, false, 35f, 60f, 25f),
                Is.EqualTo(35f)
            );
            Assert.That(
                CameraController.CalculateTargetFov(true, false, 35f, 60f, 25f),
                Is.EqualTo(60f)
            );
            Assert.That(
                CameraController.CalculateTargetFov(false, true, 40f, 60f, 25f),
                Is.EqualTo(30f)
            );
            Assert.That(
                CameraController.CalculateTargetFov(true, true, 40f, 60f, 0f),
                Is.EqualTo(60f)
            );
            Assert.That(
                ResponsiveMainMenuLayout.ResolveGamepadAxis(Vector2.up, 0f, 0f),
                Is.EqualTo(Vector2.up)
            );
            Assert.That(
                ResponsiveMainMenuLayout.ResolveGamepadAxis(Vector2.right, 0f, 0f),
                Is.EqualTo(Vector2.right)
            );
            Assert.That(
                ResponsiveMainMenuLayout.ResolveGamepadAxis(Vector2.zero, -1f, 1f),
                Is.EqualTo(new Vector2(1f, -1f))
            );
        }

        [Test]
        public void CameraLifecycleHandlesMissingAndLateTargets()
        {
            GameObject host = new("Coverage camera", typeof(Camera), typeof(CameraController));
            GameObject target = new("Coverage camera target");
            try
            {
                CameraController controller = host.GetComponent<CameraController>();
                controller.landscapeFOV = 0f;
                controller.forceMobileZoomInEditor = true;
                Invoke(controller, "Start");
                controller.target = null;
                Invoke(controller, "LateUpdate");
                controller.target = target.transform;
                Set(controller, "initialized", false);
                Invoke(controller, "LateUpdate");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ResponsiveLightHandlesEveryLifecycleAndAspectState()
        {
            ResponsiveLight2D.CalculateRadii(2f, 12f, 2f, 0.35f, out float outer, out float inner);
            Assert.That(outer, Is.EqualTo(12f));
            Assert.That(inner, Is.EqualTo(2f));
            ResponsiveLight2D.CalculateRadii(0.5f, 12f, 2f, 0.35f, out outer, out inner);
            Assert.That(outer, Is.LessThan(12f));
            Assert.That(inner, Is.LessThan(2f));

            GameObject missing = new("Coverage missing light", typeof(ResponsiveLight2D));
            System.Type lightType = typeof(ResponsiveLight2D)
                .GetField("light2D", PrivateInstance)
                .FieldType;
            GameObject host = new("Coverage light", lightType, typeof(ResponsiveLight2D));
            try
            {
                ResponsiveLight2D absent = missing.GetComponent<ResponsiveLight2D>();
                LogAssert.Expect(LogType.Error, "[ResponsiveLight2D] No Light2D component found!");
                Invoke(absent, "Start");
                Invoke(absent, "Update");

                ResponsiveLight2D responsive = host.GetComponent<ResponsiveLight2D>();
                responsive.baseLandscapeOuterRadius = 0f;
                responsive.baseLandscapeInnerRadius = 0f;
                Invoke(responsive, "Start");
                Invoke(responsive, "Update");
            }
            finally
            {
                Object.DestroyImmediate(missing);
                Object.DestroyImmediate(host);
            }
        }

        private static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    }
}
