using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class TorchFireRenderTests
    {
        private Camera[] suspendedCameras;

        [SetUp]
        public void SuspendSceneCameras()
        {
            suspendedCameras = Object
                .FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(camera => camera.enabled)
                .ToArray();
            foreach (Camera camera in suspendedCameras)
                camera.enabled = false;
        }

        [UnityTearDown]
        public IEnumerator ResumeSceneCameras()
        {
            // Let the restored pipeline initialize before existing URP cameras render again.
            yield return null;
            yield return null;
            foreach (Camera camera in suspendedCameras)
                if (camera != null)
                    camera.enabled = true;
        }

        [UnityTest]
        public IEnumerator TorchProducesDetailedWarmFlamesInBothPipelines()
        {
            RenderPipelineAsset originalPipeline = QualitySettings.renderPipeline;
            float originalLodBias = QualitySettings.lodBias;
            byte[] qualitySettings = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Torch render verification") { layer = 31 };
            SceneManager.MoveGameObjectToScene(root, scene);
            var target = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGBHalf);
            var pixels = new Texture2D(768, 768, TextureFormat.RGBAFloat, false, true);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var reference = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/Settings/Rendering/HDRP/BROcoli HDRP High Volume.asset"
            );
            foreach (VolumeComponent component in reference.components)
                if (component.GetType().Name == "Exposure")
                    profile.components.Add(Object.Instantiate(component));
            Volume volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10000f;
            volume.sharedProfile = profile;
            var camera = new GameObject("Fire camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.scene = scene;
            camera.enabled = false;
            // Both front ends see an explicit camera configuration, independent of whatever
            // main-menu or dungeon camera a preceding scene smoke test left loaded.
            var urp =
                camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urp.volumeLayerMask = 1 << 31;
            urp.requiresColorTexture = true;
            urp.requiresDepthTexture = true;
            Type hdrpType = Type.GetType(
                "UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime"
            );
            if (hdrpType != null)
            {
                Component hdrp = camera.gameObject.AddComponent(hdrpType);
                hdrpType.GetField("volumeLayerMask").SetValue(hdrp, (LayerMask)(1 << 31));
                var clearMode = hdrpType.GetField("clearColorMode");
                clearMode.SetValue(hdrp, Enum.Parse(clearMode.FieldType, "Color"));
                hdrpType
                    .GetField("backgroundColorHDR")
                    .SetValue(hdrp, new Color(0.018f, 0.024f, 0.035f));
            }
            // Exercise camera-relative rendering rather than only the world origin.
            Vector3 offset = new(1000f, 0f, 1000f);
            camera.transform.position = offset + new Vector3(0f, 2.55f, -3f);
            camera.transform.LookAt(offset + new Vector3(0f, 2.55f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = 1.15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.024f, 0.035f);
            camera.cullingMask = 1 << 31;
            camera.allowHDR = true;
            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.targetTexture = target;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonTorch.prefab"
                );
                Assert.That(prefab, Is.Not.Null);
                GameObject torch = Object.Instantiate(
                    prefab,
                    offset,
                    Quaternion.identity,
                    root.transform
                );
                var fire = torch.GetComponent<TorchFireVfx>() ?? torch.AddComponent<TorchFireVfx>();
                fire.Initialize();
                foreach (Transform child in torch.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 31;
                foreach (Light light in torch.GetComponentsInChildren<Light>())
                    light.enabled = false;
                foreach (MeshRenderer mesh in torch.GetComponentsInChildren<MeshRenderer>())
                    mesh.enabled = false;
                var layers = torch
                    .GetComponentsInChildren<ParticleSystemRenderer>()
                    .Where(renderer => renderer.enabled)
                    .ToArray();
                Assert.That(layers.Length, Is.EqualTo(5));
                foreach (var renderer in layers)
                    renderer.GetComponent<ParticleSystem>().Simulate(1.8f, false, true);

                foreach (string pipeline in new[] { "URP", "HDRP" })
                {
                    QualitySettings.renderPipeline =
                        pipeline == "URP"
                            ? GraphicsSettings.defaultRenderPipeline
                            : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                                "Assets/Settings/Rendering/HDRP/BROcoli HDRP High.asset"
                            );
                    yield return null;
                    yield return null;
                    foreach (var layer in layers)
                        layer.enabled = false;
                    RenderPipeline.SubmitRenderRequest(
                        camera,
                        new RenderPipeline.StandardRequest { destination = target }
                    );
                    Color[] background = Read(target, pixels);
                    foreach (var layer in layers)
                        layer.enabled = true;
                    RenderPipeline.SubmitRenderRequest(
                        camera,
                        new RenderPipeline.StandardRequest { destination = target }
                    );
                    Color[] firePixels = Read(target, pixels);
                    int warm = 0;
                    int blue = 0;
                    int nonFinite = 0;
                    float peak = 0f;
                    foreach (var pair in firePixels.Zip(background, (a, b) => a - b))
                    {
                        peak = Mathf.Max(peak, pair.maxColorComponent);
                        if (pair.r > 0.08f && pair.r > pair.b * 1.4f)
                            warm++;
                        if (pair.b > 0.025f && pair.b > pair.r * 1.4f)
                            blue++;
                        if (float.IsNaN(pair.r) || float.IsInfinity(pair.r))
                            nonFinite++;
                    }
                    Directory.CreateDirectory("build/verification");
                    File.WriteAllBytes(
                        $"build/verification/torch-fire-{pipeline}.png",
                        pixels.EncodeToPNG()
                    );
                    Assert.That(
                        warm,
                        Is.GreaterThan(500),
                        pipeline + " needs a visible warm flame silhouette"
                    );
                    Assert.That(blue, Is.GreaterThan(10), pipeline + " needs a blue ignition base");
                    Assert.That(
                        nonFinite,
                        Is.Zero,
                        pipeline + " must not emit NaN or infinite light"
                    );
                    Assert.That(
                        peak,
                        Is.GreaterThan(0.3f),
                        pipeline + " must retain emissive highlights"
                    );
                    Assert.That(
                        warm,
                        Is.LessThan(firePixels.Length / 4),
                        "fire stays compact without a broad glowing orb"
                    );
                    Shader shader = layers[0].sharedMaterial.shader;
                    Assert.That(ShaderUtil.GetShaderMessages(shader), Is.Empty, pipeline);
                }
            }
            finally
            {
                QualitySettings.renderPipeline = originalPipeline;
                QualitySettings.lodBias = originalLodBias;
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var component in profile.components)
                    Object.DestroyImmediate(component);
                Object.DestroyImmediate(profile);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                File.WriteAllBytes("ProjectSettings/QualitySettings.asset", qualitySettings);
            }
        }

        private static Color[] Read(RenderTexture target, Texture2D pixels)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            return pixels.GetPixels();
        }
    }
}
