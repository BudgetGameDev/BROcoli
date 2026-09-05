using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class PickupGlowRenderTests
    {
        [UnityTest]
        public IEnumerator GlowAddsLightInBothPipelines()
        {
            RenderPipelineAsset originalQuality = QualitySettings.renderPipeline;
            float originalLodBias = QualitySettings.lodBias;
            byte[] qualitySettings = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            var root = new GameObject("Pickup glow preview");
            var target = new RenderTexture(1200, 650, 24, RenderTextureFormat.ARGBHalf);
            var pixels = new Texture2D(1200, 650, TextureFormat.RGBAFloat, false, true);
            var shells = new List<MeshRenderer>();
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var referenceProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/Settings/Rendering/HDRP/BROcoli HDRP High Volume.asset"
            );
            foreach (VolumeComponent component in referenceProfile.components)
                if (component.GetType().Name == "Exposure")
                    profile.components.Add(Object.Instantiate(component));
            Volume volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10000f;
            volume.sharedProfile = profile;
            // Away from world origin to also exercise HDRP's camera-relative transforms.
            Vector3 offset = new(1000f, 0f, 1000f);
            Camera camera = new GameObject("Preview camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.position = offset + new Vector3(0f, 8f, -9f);
            camera.transform.LookAt(offset + new Vector3(0f, 0.3f, 1.2f));
            camera.orthographic = true;
            camera.orthographicSize = 3.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.018f, 0.032f);
            camera.cullingMask = 1 << 31;
            camera.allowHDR = true;
            camera.targetTexture = target;
            Light light = new GameObject("Preview light").AddComponent<Light>();
            light.transform.SetParent(root.transform);
            light.type = LightType.Directional;
            light.intensity = 2f;
            light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
            try
            {
                foreach (
                    PickupVisual3D.ModelKind kind in Enum.GetValues(
                        typeof(PickupVisual3D.ModelKind)
                    )
                )
                {
                    int index = (int)kind;
                    var pickup = new GameObject(kind.ToString()) { layer = 31 };
                    pickup.transform.SetParent(root.transform);
                    pickup.transform.position =
                        offset + new Vector3((index % 6 - 2.5f) * 1.8f, 0f, index / 6 * 2.5f);
                    var visual = pickup.AddComponent<PickupVisual3D>();
                    typeof(PickupVisual3D)
                        .GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(visual, new object[] { kind });
                    foreach (
                        MeshRenderer renderer in pickup.GetComponentsInChildren<MeshRenderer>()
                    )
                        if (
                            renderer.name == PickupVisual3D.GlowCoreName
                            || renderer.name == PickupVisual3D.GlowHaloName
                        )
                            shells.Add(renderer);
                }

                foreach (string pipeline in new[] { "URP", "HDRP" })
                {
                    QualitySettings.renderPipeline =
                        pipeline == "URP"
                            ? GraphicsSettings.defaultRenderPipeline
                            : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                                "Assets/Settings/Rendering/HDRP/BROcoli HDRP High.asset"
                            );
                    Assert.That(QualitySettings.renderPipeline, Is.Not.Null);
                    yield return null;
                    yield return null;
                    if (pipeline == "HDRP")
                    {
                        // HDRP initializes additional light data with outdoor defaults.
                        Type dataType = Type.GetType(
                            "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData, Unity.RenderPipelines.HighDefinition.Runtime"
                        );
                        if (light.GetComponent(dataType) == null)
                            light.gameObject.AddComponent(dataType);
                        light.intensity = 600f;
                    }
                    foreach (MeshRenderer shell in shells)
                        shell.enabled = false;
                    RenderPipeline.SubmitRenderRequest(
                        camera,
                        new RenderPipeline.StandardRequest { destination = target }
                    );
                    Color[] without = Read(target, pixels);
                    foreach (MeshRenderer shell in shells)
                        shell.enabled = true;
                    RenderPipeline.SubmitRenderRequest(
                        camera,
                        new RenderPipeline.StandardRequest { destination = target }
                    );
                    Color[] with = Read(target, pixels);
                    int litPixels = 0;
                    float peak = 0f;
                    for (int i = 0; i < with.Length; i++)
                    {
                        float added = (with[i] - without[i]).maxColorComponent;
                        if (added > 0.03f)
                            litPixels++;
                        peak = Mathf.Max(peak, added);
                    }
                    Directory.CreateDirectory("build/verification");
                    File.WriteAllBytes(
                        $"build/verification/pickup-glow-{pipeline}.png",
                        pixels.EncodeToPNG()
                    );
                    Assert.That(
                        litPixels,
                        Is.GreaterThan(500),
                        $"{pipeline}: glow must actually reach the render target"
                    );
                    // HDRP's final render request includes the SDR output transform.
                    // Check visible contribution here; scene-linear HDR colors are tested separately.
                    Assert.That(
                        peak,
                        Is.GreaterThan(0.25f),
                        $"{pipeline}: glow must remain visible after output conversion"
                    );
                    Shader shader = Resources.Load<Shader>(PickupVisual3D.GlowShaderResource);
                    Assert.That(ShaderUtil.GetShaderMessages(shader), Is.Empty, pipeline);
                }
            }
            finally
            {
                QualitySettings.renderPipeline = originalQuality;
                QualitySettings.lodBias = originalLodBias;
                Object.DestroyImmediate(root);
                foreach (VolumeComponent component in profile.components)
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
