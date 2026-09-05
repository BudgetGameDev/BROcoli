using System.Collections;
using System.Collections.Generic;
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

        [SetUp]
        public void SaveSelectedPipeline() =>
            previousQualityPipeline = QualitySettings.renderPipeline;

        [UnityTearDown]
        public IEnumerator RestoreSelectedPipeline()
        {
            QualitySettings.renderPipeline = previousQualityPipeline;
            yield return null;
            yield return null;
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

            QualitySettings.renderPipeline = universal;
            yield return null;
            yield return null;
            (Color surface, Color flame) urp = RenderFixture(false);

            QualitySettings.renderPipeline = highDefinition;
            yield return null;
            yield return null;
            (Color surface, Color flame) hdrp = RenderFixture(true);

            Assert.That(
                urp.surface.r,
                Is.GreaterThan(0.001f),
                "The fixture must actually receive direct light."
            );
            for (int channel = 0; channel < 3; channel++)
            {
                // Disney versus URP diffuse differs slightly at the same 18% linear reference.
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

        private static (Color surface, Color flame) RenderFixture(bool highDefinition)
        {
            Assert.That(
                GraphicsSettings.currentRenderPipeline is HD.HDRenderPipelineAsset,
                Is.EqualTo(highDefinition)
            );
            using var ambient = new AmbientIsolation();
            Scene scene = EditorSceneManager.NewPreviewScene();
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
                    hideFlags = HideFlags.DontUnloadUnusedAsset,
                };
                owned.Add(target);
                Assert.That(target.Create(), Is.True);
                Color lit = Sample(camera, target);

                light.enabled = false;
                Material flame = CreateMaterial("BROcoli/Flame", owned);
                flame.SetColor("_BaseColor", new Color(8f, 4f, 1f, 1f));
                flame.SetFloat("_EmissiveIntensity", 1f);
                flame.SetTexture("_BaseMap", Texture2D.whiteTexture);
                renderer.sharedMaterial = flame;
                return (lit, Sample(camera, target));
            }
            finally
            {
                RenderTexture.active = previousTarget;
                EditorSceneManager.ClosePreviewScene(scene);
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

        private static GameObject CreateObject(string name, Scene scene)
        {
            var value = new GameObject(name) { layer = FixtureLayer };
            SceneManager.MoveGameObjectToScene(value, scene);
            return value;
        }

        private sealed class AmbientIsolation : System.IDisposable
        {
            private readonly AmbientMode mode = RenderSettings.ambientMode;
            private readonly Color light = RenderSettings.ambientLight;
            private readonly Color sky = RenderSettings.ambientSkyColor;
            private readonly Color equator = RenderSettings.ambientEquatorColor;
            private readonly Color ground = RenderSettings.ambientGroundColor;
            private readonly float intensity = RenderSettings.ambientIntensity;
            private readonly float reflection = RenderSettings.reflectionIntensity;
            private readonly SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;

            public AmbientIsolation()
            {
                // Preview scenes still see the global ambient probe. A colored launcher sky
                // would overwhelm the white direct-light fixture and invalidate the comparison.
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.black;
                RenderSettings.ambientIntensity = 0f;
                RenderSettings.reflectionIntensity = 0f;
                RenderSettings.ambientProbe = default;
            }

            public void Dispose()
            {
                RenderSettings.ambientMode = mode;
                RenderSettings.ambientLight = light;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientIntensity = intensity;
                RenderSettings.reflectionIntensity = reflection;
                RenderSettings.ambientProbe = probe;
            }
        }

        private static Material CreateMaterial(string shaderName, List<Object> owned)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, shaderName);
            var material = new Material(shader);
            owned.Add(material);
            return material;
        }

        private static Color Sample(Camera camera, RenderTexture target)
        {
            RenderPipeline.SubmitRenderRequest(
                camera,
                new RenderPipeline.StandardRequest { destination = target }
            );
            RenderTexture.active = target;
            var readback = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true);
            try
            {
                readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                readback.Apply();
                return readback.GetPixel(Size / 2, Size / 2);
            }
            finally
            {
                Object.DestroyImmediate(readback);
            }
        }
    }
}
