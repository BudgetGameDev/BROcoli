using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HD = UnityEngine.Rendering.HighDefinition;
using URP = UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Rendering.HighDefinition.Tests
{
    public sealed partial class LightingPipelineParityTests
    {
        private const string HighDefinitionAsset =
            "Assets/Settings/Rendering/HDRP/BROcoli HDRP High.asset";
        private const int FixtureLayer = 31;
        private const int Size = 128;
        private RenderPipelineAsset previousQualityPipeline;
        private Camera[] suspendedCameras;

        [SetUp]
        public void SaveSelectedPipeline()
        {
            previousQualityPipeline = QualitySettings.renderPipeline;
            suspendedCameras = Object
                .FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(camera => camera.enabled)
                .ToArray();
            foreach (Camera camera in suspendedCameras)
                camera.enabled = false;
        }

        [UnityTearDown]
        public IEnumerator RestoreSelectedPipeline()
        {
            QualitySettings.renderPipeline = previousQualityPipeline;
            yield return null;
            yield return null;
            foreach (Camera camera in suspendedCameras)
                if (camera != null)
                    camera.enabled = true;
        }

        [UnityTest]
        public IEnumerator PhysicalSurfaceAndUnlitHdrFlameRetainTheirSceneLinearValuesAcrossPipelines()
        {
            IgnoreWhenHdrpIsUnsupported();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore(
                    "Requires float GPU rendering; an SDR screenshot cannot validate HDR lighting."
                );

            var universal = AssetDatabase.LoadAssetAtPath<URP.UniversalRenderPipelineAsset>(
                "Assets/3dRenderer.asset"
            );
            var highDefinition = AssetDatabase.LoadAssetAtPath<HD.HDRenderPipelineAsset>(
                HighDefinitionAsset
            );
            Assert.That(universal, Is.Not.Null);
            Assert.That(highDefinition, Is.Not.Null);

            // Creating/closing a preview scene can unload editor render resources. Keep the
            // scene alive across pipeline initialization and both float readbacks.
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                QualitySettings.renderPipeline = universal;
                yield return null;
                yield return null;
                (Color surface, Color flame) urp = RenderFixture(false, scene);

                QualitySettings.renderPipeline = highDefinition;
                yield return null;
                yield return null;
                (Color surface, Color flame) hdrp = RenderFixture(true, scene);

                Assert.That(
                    urp.surface.r,
                    Is.GreaterThan(0.001f),
                    "The fixture must actually receive direct light."
                );
                for (int channel = 0; channel < 3; channel++)
                {
                    // Compare the same 18% linear reference. Disney versus URP diffuse differs
                    // slightly; near-black albedo would amplify their specular differences.
                    Assert.That(
                        hdrp.surface[channel],
                        Is.EqualTo(urp.surface[channel]).Within(urp.surface[channel] * 0.05f),
                        $"Scene-linear surface channel {channel}: URP {urp.surface}, HDRP {hdrp.surface}"
                    );
                    float expectedFlame = new Color(8f, 4f, 1f)[channel];
                    Assert.That(
                        urp.flame[channel],
                        Is.EqualTo(expectedFlame).Within(expectedFlame * 0.01f),
                        $"URP flame channel {channel} must remain above display white in RGBAFloat."
                    );
                    Assert.That(
                        hdrp.flame[channel],
                        Is.EqualTo(expectedFlame).Within(expectedFlame * 0.01f),
                        $"HDRP flame channel {channel} must not be multiplied by physical-light exposure or emitted twice."
                    );
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static (Color surface, Color flame) RenderFixture(bool highDefinition, Scene scene)
        {
            Assert.That(
                GraphicsSettings.currentRenderPipeline is HD.HDRenderPipelineAsset,
                Is.EqualTo(highDefinition)
            );
            using var ambient = new AmbientIsolation();
            var owned = new List<Object>();
            RenderTexture previousTarget = RenderTexture.active;
            VolumeProfile profile = null;
            try
            {
                GameObject cameraObject = CreateObject("Float lighting fixture camera", scene);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 0.5f;
                camera.aspect = 1f;
                camera.transform.position = new Vector3(0f, 0f, -5f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << FixtureLayer;
                camera.allowHDR = true;
                camera.allowMSAA = false;
                camera.allowDynamicResolution = false;
                if (highDefinition)
                {
                    var data = cameraObject.AddComponent<HD.HDAdditionalCameraData>();
                    data.volumeLayerMask = 1 << FixtureLayer;
                    data.antialiasing = HD.HDAdditionalCameraData.AntialiasingMode.None;
                    data.dithering = false;
                    data.clearColorMode = HD.HDAdditionalCameraData.ClearColorMode.Color;
                    data.backgroundColorHDR = Color.black;
                }
                else
                {
                    var data = cameraObject.AddComponent<URP.UniversalAdditionalCameraData>();
                    data.volumeLayerMask = 1 << FixtureLayer;
                    data.renderPostProcessing = false;
                    data.antialiasing = URP.AntialiasingMode.None;
                }

                Volume volume = CreateObject("Float lighting fixture volume", scene)
                    .AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = float.MaxValue;
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.sharedProfile = profile;
                if (highDefinition)
                {
                    var exposure = profile.Add<HD.Exposure>(true);
                    exposure.mode.Override(HD.ExposureMode.Fixed);
                    exposure.fixedExposure.Override(
                        SceneLuminanceBudget.Dungeon.FixedExposureEv100
                    );
                    exposure.compensation.Override(0f);
                    profile.Add<HD.Tonemapping>(true).mode.Override(HD.TonemappingMode.None);
                    profile.Add<HD.Bloom>(true).intensity.Override(0f);
                    profile.Add<ImpressionistBloom>(true).intensity.Override(0f);
                    profile.Add<HD.Fog>(true).enabled.Override(false);
                    profile.Add<HD.VisualEnvironment>(true).skyType.Override(0);
                    profile.Add<HD.ScreenSpaceAmbientOcclusion>(true).intensity.Override(0f);
                }

                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                SceneManager.MoveGameObjectToScene(quad, scene);
                quad.layer = FixtureLayer;
                Mesh mesh = Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);
                owned.Add(mesh);
                var colors = new Color[mesh.vertexCount];
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = Color.white;
                mesh.colors = colors;
                quad.GetComponent<MeshFilter>().sharedMesh = mesh;
                Material surface = CreateMaterial("BROcoli/Surface", owned);
                // The non-HDR graph color property expects an sRGB-authored color.
                surface.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.18f, 1f).gamma);
                surface.SetTexture("_BaseMap", Texture2D.whiteTexture);
                surface.SetFloat("_Metallic", 0f);
                surface.SetFloat("_Smoothness", 0f);
                Renderer renderer = quad.GetComponent<Renderer>();
                renderer.sharedMaterial = surface;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                GameObject lightObject = CreateObject("Float lighting fixture point", scene);
                lightObject.transform.position = new Vector3(0f, 0f, -2f);
                Light light = lightObject.AddComponent<Light>();
                light.cullingMask = 1 << FixtureLayer;
                light.shadows = LightShadows.None;
                var spec = new PunctualLightSpec(67.5f, 2f, 8f, Color.white);
                ILightingFrontEnd frontEnd = highDefinition
                    ? new HighDefinitionLightingFrontEnd()
                    : new Universal.UniversalLightingFrontEnd();
                frontEnd.ConfigurePunctual(
                    light,
                    spec,
                    SceneLuminanceBudget.AuthoringPaperWhiteNits
                );

                var target = new RenderTexture(
                    Size,
                    Size,
                    24,
                    RenderTextureFormat.ARGBFloat,
                    RenderTextureReadWrite.Linear
                )
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                owned.Add(target);
                Assert.That(target.Create(), Is.True);
                // Keep a native scene reference throughout all submissions. Shader imports
                // may unload unused assets while the Editor services a render request; a
                // managed local and request object alone do not root this render texture.
                camera.targetTexture = target;
                var readback = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                owned.Add(readback);
                Color lit = Sample(camera, target, readback, 4);

                light.enabled = false;
                Material flame = CreateMaterial("BROcoli/Flame", owned);
                flame.SetColor("_BaseColor", new Color(8f, 4f, 1f, 1f));
                flame.SetFloat("_EmissiveIntensity", 1f);
                flame.SetTexture("_BaseMap", Texture2D.whiteTexture);
                renderer.sharedMaterial = flame;
                return (lit, Sample(camera, target, readback, 2));
            }
            finally
            {
                RenderTexture.active = previousTarget;
                foreach (GameObject root in scene.GetRootGameObjects())
                    Object.DestroyImmediate(root);
                if (profile != null)
                {
                    foreach (VolumeComponent component in profile.components)
                        Object.DestroyImmediate(component);
                    Object.DestroyImmediate(profile);
                }
                foreach (Object value in owned)
                    Object.DestroyImmediate(value);
            }
        }
    }
}
