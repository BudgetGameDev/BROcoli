using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    public sealed partial class GameDisplaySettingsTests
    {
        [Test]
        public void HdrUsesCameraSpaceUiAndRestoresTheOriginalCanvasSetup()
        {
            GameObject driverRoot = new("HDR Canvas Driver");
            GameObject cameraRoot = new("HDR Canvas Camera");
            GameObject canvasRoot = new("HDR Canvas", typeof(Canvas));
            try
            {
                var driver = driverRoot.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                Camera camera = cameraRoot.AddComponent<Camera>();
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 100f;
                Canvas canvas = canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.planeDistance = 42f;

                driver.UpdateCanvasComposition(true, new[] { canvas }, camera);

                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
                Assert.That(canvas.worldCamera, Is.SameAs(camera));
                Assert.That(canvas.planeDistance, Is.EqualTo(1f));

                driver.UpdateCanvasComposition(false, new[] { canvas }, camera);

                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(canvas.worldCamera, Is.Null);
                Assert.That(canvas.planeDistance, Is.EqualTo(42f));
            }
            finally
            {
                Object.DestroyImmediate(canvasRoot);
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(driverRoot);
            }
        }

        [Test]
        public void HdrCanvasCompositionLeavesWorldAndExistingCameraCanvasesAlone()
        {
            GameObject driverRoot = new("HDR Canvas Driver");
            GameObject cameraRoot = new("HDR Canvas Camera");
            GameObject cameraCanvasRoot = new("Camera Canvas", typeof(Canvas));
            GameObject worldCanvasRoot = new("World Canvas", typeof(Canvas));
            try
            {
                var driver = driverRoot.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                Camera camera = cameraRoot.AddComponent<Camera>();
                Canvas cameraCanvas = cameraCanvasRoot.GetComponent<Canvas>();
                Canvas worldCanvas = worldCanvasRoot.GetComponent<Canvas>();
                cameraCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                cameraCanvas.worldCamera = camera;
                worldCanvas.renderMode = RenderMode.WorldSpace;

                driver.UpdateCanvasComposition(true, new[] { cameraCanvas, worldCanvas }, camera);

                Assert.That(cameraCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
                Assert.That(cameraCanvas.worldCamera, Is.SameAs(camera));
                Assert.That(worldCanvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            }
            finally
            {
                Object.DestroyImmediate(worldCanvasRoot);
                Object.DestroyImmediate(cameraCanvasRoot);
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(driverRoot);
            }
        }
    }
}
