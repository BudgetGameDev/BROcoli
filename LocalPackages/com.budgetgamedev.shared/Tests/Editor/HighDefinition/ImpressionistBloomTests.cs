using System.Collections;
using BudgetGameDev.Shared.Rendering.HighDefinition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Rendering.HighDefinition.Tests
{
    public sealed class ImpressionistBloomTests
    {
        private RenderPipelineAsset previousQualityPipeline;
        private bool changedPipeline;

        [UnitySetUp]
        public IEnumerator UseHighDefinitionShaderSubshader()
        {
            previousQualityPipeline = QualitySettings.renderPipeline;
            changedPipeline = GraphicsSettings.currentRenderPipeline is not HDRenderPipelineAsset;
            if (changedPipeline)
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(
                    "Assets/Settings/Rendering/HDRP/BROcoli HDRP High.asset"
                );
                Assert.That(
                    pipeline,
                    Is.Not.Null,
                    "The GPU regression requires the project's HDRP asset."
                );
                QualitySettings.renderPipeline = pipeline;
                // Shader SubShader selection follows the active pipeline on the next render loop.
                yield return null;
                yield return null;
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreSelectedPipeline()
        {
            if (changedPipeline)
            {
                QualitySettings.renderPipeline = previousQualityPipeline;
                yield return null;
            }
        }

        [TestCase(8f)]
        [TestCase(0.05f)]
        public void UniformFieldUsesAdditiveThresholdAndRetainsHdrAndAlpha(float value)
        {
            Color[] pixels = Render(
                64,
                64,
                64,
                64,
                (_, _) => new Color(value, value, value, 0.37f)
            );
            float threshold = Mathf.GammaToLinearSpace(0.85f);
            float expected = value < threshold * 0.5f ? value : value + 1.35f * (value - threshold);
            foreach (Color pixel in pixels)
            {
                Assert.That(
                    pixel.r,
                    Is.EqualTo(expected).Within(Mathf.Max(0.003f, expected * 0.004f))
                );
                Assert.That(pixel.g, Is.EqualTo(pixel.r).Within(0.003f));
                Assert.That(pixel.b, Is.EqualTo(pixel.r).Within(0.003f));
                Assert.That(pixel.a, Is.EqualTo(0.37f).Within(0.001f));
            }
        }

        [Test]
        public void BrightCoreKeepsItsEnergyAndSpillsIntoDarkSurroundings()
        {
            Color[] pixels = Render(
                64,
                64,
                64,
                64,
                (x, y) =>
                    x >= 28 && x < 36 && y >= 28 && y < 36
                        ? new Color(16f, 8f, 2f, 1f)
                        : Color.black
            );
            Assert.That(pixels[32 * 64 + 32].r, Is.GreaterThan(16f));
            Assert.That(pixels[32 * 64 + 25].r, Is.GreaterThan(0.02f));
            Assert.That(pixels[32 * 64 + 25].r, Is.GreaterThan(pixels[32 * 64 + 25].g));
        }

        [Test]
        public void SmallerViewportDoesNotSampleBrightStaleBackingPixels()
        {
            Color[] pixels = Render(
                128,
                128,
                64,
                64,
                (x, y) =>
                    x < 64 && y < 64
                        ? new Color(0.05f, 0.05f, 0.05f, 1f)
                        : new Color(100f, 100f, 100f, 1f)
            );
            foreach (Color pixel in pixels)
                Assert.That(pixel.r, Is.EqualTo(0.05f).Within(0.001f));
        }

        private static Color[] Render(
            int backingWidth,
            int backingHeight,
            int width,
            int height,
            System.Func<int, int, Color> colorAt
        )
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore(
                    "Requires a GPU; an SDR screenshot cannot validate floating point bloom."
                );

            var input = new Texture2D(
                backingWidth,
                backingHeight,
                TextureFormat.RGBAFloat,
                false,
                true
            );
            var colors = new Color[backingWidth * backingHeight];
            for (int y = 0; y < backingHeight; y++)
            for (int x = 0; x < backingWidth; x++)
                colors[y * backingWidth + x] = colorAt(x, y);
            input.SetPixels(colors);
            input.Apply();
            RTHandle source = RTHandles.Alloc(
                backingWidth,
                backingHeight,
                slices: TextureXR.slices,
                dimension: TextureXR.dimension,
                colorFormat: GraphicsFormat.R16G16B16A16_SFloat
            );
            RTHandle destination = RTHandles.Alloc(
                width,
                height,
                slices: TextureXR.slices,
                dimension: TextureXR.dimension,
                colorFormat: GraphicsFormat.R16G16B16A16_SFloat
            );
            var upload = new RenderTexture(
                backingWidth,
                backingHeight,
                0,
                RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear
            );
            var bloom = ScriptableObject.CreateInstance<ImpressionistBloom>();
            var readback = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            var command = new CommandBuffer();
            RenderTexture previous = RenderTexture.active;
            try
            {
                // HDRP uses one-slice arrays even for non-XR cameras on Metal / DX12.
                // A plain Texture2D bound to TEXTURE2D_X silently samples a fallback texture.
                upload.Create();
                Graphics.Blit(input, upload);
                for (int slice = 0; slice < TextureXR.slices; slice++)
                    Graphics.CopyTexture(upload, 0, 0, source.rt, slice, 0);
                bloom.Setup();
                bloom.intensity.value = 1.35f;
                Assert.That(bloom.IsActive(), Is.True, "The retained bloom shader must load.");
                bloom.RenderBloom(command, source, destination, width, height);
                Graphics.ExecuteCommandBuffer(command);
                Graphics.SetRenderTarget(destination.rt, 0, CubemapFace.Unknown, 0);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                return readback.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                command.Release();
                bloom.Cleanup();
                source.Release();
                destination.Release();
                Object.DestroyImmediate(bloom);
                Object.DestroyImmediate(input);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(upload);
            }
        }
    }
}
