using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Shared.Rendering.HighDefinition.Tests
{
    public sealed partial class LightingPipelineParityTests
    {
        private static void IgnoreWhenHdrpIsUnsupported()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
                Assert.Ignore(
                    "HDRP rendering is unavailable while WebGL is the active build target."
                );
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

        private static Color Sample(
            Camera camera,
            RenderTexture target,
            Texture2D readback,
            int renders
        )
        {
            // HDRP initializes these shared defaults; URP imports them without initializing
            // them. Preview-scene cleanup can unload their native textures between tests.
            // Use the public initializer to recreate unloaded defaults before either render.
            var clearShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Packages/com.unity.render-pipelines.high-definition/Runtime/Core/CoreResources/ClearUIntTextureArray.compute"
            );
            Assert.That(clearShader, Is.Not.Null);
            CommandBuffer initialize = CommandBufferPool.Get("Lighting fixture XR defaults");
            try
            {
                TextureXR.Initialize(initialize, clearShader);
                Graphics.ExecuteCommandBuffer(initialize);
            }
            finally
            {
                CommandBufferPool.Release(initialize);
            }

            // Bounded history warm-up; the same number of submissions runs on each pipeline.
            for (int i = 0; i < renders; i++)
                RenderPipeline.SubmitRenderRequest(
                    camera,
                    new RenderPipeline.StandardRequest { destination = target }
                );
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            readback.Apply();
            return readback.GetPixel(Size / 2, Size / 2);
        }
    }
}
