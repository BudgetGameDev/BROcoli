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
            GameObject secondCameraRoot = new("Second HDR Canvas Camera");
            GameObject canvasRoot = new("HDR Canvas", typeof(Canvas));
            try
            {
                var driver = driverRoot.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                Camera camera = cameraRoot.AddComponent<Camera>();
                Camera secondCamera = secondCameraRoot.AddComponent<Camera>();
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 100f;
                Canvas canvas = canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.planeDistance = 42f;

                driver.UpdateCanvasComposition(true, new[] { canvas }, camera);

                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
                Assert.That(canvas.worldCamera, Is.SameAs(camera));
                Assert.That(canvas.planeDistance, Is.EqualTo(1f));

                driver.UpdateCanvasComposition(true, new[] { canvas }, secondCamera);
                Assert.That(canvas.worldCamera, Is.SameAs(secondCamera));

                driver.UpdateCanvasComposition(false, new[] { canvas }, camera);

                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(canvas.worldCamera, Is.Null);
                Assert.That(canvas.planeDistance, Is.EqualTo(42f));
            }
            finally
            {
                Object.DestroyImmediate(canvasRoot);
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(secondCameraRoot);
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

        [Test]
        public void CanvasRefreshHandlesSceneChangesDestroyedCanvasesAndCameraFallbacks()
        {
            GameObject driverRoot = new("HDR Canvas Refresh Driver");
            GameObject cameraRoot = new("HDR Canvas Refresh Camera");
            GameObject canvasRoot = new("HDR Refresh Canvas", typeof(Canvas));
            try
            {
                var driver = driverRoot.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                Camera camera = cameraRoot.AddComponent<Camera>();
                cameraRoot.tag = "MainCamera";
                Canvas canvas = canvasRoot.GetComponent<Canvas>();

                driver.Awake();
                driver.Awake();
                driver.UpdateCanvasComposition(true, new[] { canvas }, camera);
                driver.UpdateCanvasComposition(true, null, camera);
                driver.UpdateCanvasComposition(true, System.Array.Empty<Canvas>(), camera);
                driver
                    .GetType()
                    .GetField(
                        "canvasCompositionEnabled",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .SetValue(driver, true);
                driver
                    .GetType()
                    .GetMethod(
                        "OnSceneLoadedForCanvasComposition",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .Invoke(
                        driver,
                        new object[]
                        {
                            default(UnityEngine.SceneManagement.Scene),
                            UnityEngine.SceneManagement.LoadSceneMode.Single,
                        }
                    );
                driver.LateUpdate();

                Object.DestroyImmediate(canvasRoot);
                canvasRoot = null;
                driver.UpdateCanvasComposition(true, System.Array.Empty<Canvas>(), camera);
                driver.LateUpdate();

                Assert.That(
                    GameDisplaySettings.HdrDisplayDriver.HdrCanvasPlaneDistance(null),
                    Is.EqualTo(1f)
                );
                driver.OnDestroy();
            }
            finally
            {
                if (canvasRoot != null)
                    Object.DestroyImmediate(canvasRoot);
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(driverRoot);
            }
        }

        [Test]
        public void OutputCameraFallsBackPastAnUnusableMainCamera()
        {
            Camera[] existing = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            bool[] existingEnabled = System.Array.ConvertAll(existing, camera => camera.enabled);
            foreach (Camera camera in existing)
                camera.enabled = false;
            GameObject mainRoot = new("Unusable Main Camera", typeof(Camera));
            GameObject fallbackRoot = new("Fallback Camera", typeof(Camera));
            try
            {
                mainRoot.tag = "MainCamera";
                mainRoot.GetComponent<Camera>().targetTexture = new RenderTexture(1, 1, 0);
                Camera fallback = fallbackRoot.GetComponent<Camera>();
                Camera found = (Camera)
                    typeof(GameDisplaySettings.HdrDisplayDriver)
                        .GetMethod(
                            "FindOutputCamera",
                            System.Reflection.BindingFlags.Static
                                | System.Reflection.BindingFlags.NonPublic
                        )
                        .Invoke(null, null);

                Assert.That(found, Is.SameAs(fallback));
            }
            finally
            {
                Camera mainCamera = mainRoot.GetComponent<Camera>();
                RenderTexture target = mainCamera.targetTexture;
                mainCamera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(fallbackRoot);
                Object.DestroyImmediate(mainRoot);
                for (int index = 0; index < existing.Length; index++)
                    if (existing[index] != null)
                        existing[index].enabled = existingEnabled[index];
            }
        }

        [Test]
        public void DestroyedCanvasStatesCanBeRestoredOrPruned()
        {
            GameObject driverRoot = new("Destroyed canvas state driver");
            GameObject cameraRoot = new("Destroyed canvas state camera", typeof(Camera));
            GameObject restoreRoot = new("Destroyed restore canvas", typeof(Canvas));
            GameObject pruneRoot = new("Destroyed prune canvas", typeof(Canvas));
            try
            {
                var driver = driverRoot.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                Camera camera = cameraRoot.GetComponent<Camera>();
                Canvas restore = restoreRoot.GetComponent<Canvas>();
                driver.UpdateCanvasComposition(true, new[] { restore }, camera);
                Object.DestroyImmediate(restoreRoot);
                restoreRoot = null;
                driver.UpdateCanvasComposition(false, null, camera);

                Canvas prune = pruneRoot.GetComponent<Canvas>();
                driver.UpdateCanvasComposition(true, new[] { prune }, camera);
                Object.DestroyImmediate(pruneRoot);
                pruneRoot = null;
                typeof(GameDisplaySettings.HdrDisplayDriver)
                    .GetMethod(
                        "RemoveDestroyedCanvasStates",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .Invoke(driver, null);
            }
            finally
            {
                if (restoreRoot != null)
                    Object.DestroyImmediate(restoreRoot);
                if (pruneRoot != null)
                    Object.DestroyImmediate(pruneRoot);
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(driverRoot);
            }
        }
    }
}
