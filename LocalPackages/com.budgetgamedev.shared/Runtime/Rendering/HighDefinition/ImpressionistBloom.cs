using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace BudgetGameDev.Shared.Rendering.HighDefinition
{
    /// <summary>
    /// The dungeon's additive Universal bloom, before HDRP's grade and ACES. HDRP's native
    /// veiling glare removes energy from highlights; this preserves the source and adds its halo.
    /// </summary>
    [Serializable, VolumeComponentMenu("Post-processing/Custom/BROcoli Impressionist Bloom")]
    public sealed class ImpressionistBloom : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        public MinFloatParameter intensity = new(0f, 0f);
        public MinFloatParameter threshold = new(0.85f, 0f);
        public ClampedFloatParameter scatter = new(0.72f, 0f, 1f);
        public ClampedIntParameter maxIterations = new(6, 1, 6);

        private const int MaximumMips = 6;
        private readonly RTHandle[] down = new RTHandle[MaximumMips];
        private readonly RTHandle[] up = new RTHandle[MaximumMips];
        private Material material;
        private MaterialPropertyBlock properties;

        private static readonly int SourceId = Shader.PropertyToID("_BloomSource");
        private static readonly int SourceSizeId = Shader.PropertyToID("_BloomSourceSize");
        private static readonly int SourceScaleId = Shader.PropertyToID("_BloomSourceScale");
        private static readonly int LowId = Shader.PropertyToID("_BloomLow");
        private static readonly int LowSizeId = Shader.PropertyToID("_BloomLowSize");
        private static readonly int ParamsId = Shader.PropertyToID("_BloomSettings");

        public override CustomPostProcessInjectionPoint injectionPoint =>
            CustomPostProcessInjectionPoint.BeforePostProcess;

        public bool IsActive() => material != null && intensity.value > 0f;

        public override void Setup()
        {
            // Resources retains the shader in players without an Always Included entry.
            Shader shader = Resources.Load<Shader>("Brocoli/ImpressionistBloom");
            if (shader != null)
                material = CoreUtils.CreateEngineMaterial(shader);
            else
                Debug.LogError("BROcoli additive bloom shader is missing from Resources.");
            properties = new MaterialPropertyBlock();
        }

        public override void Render(
            CommandBuffer cmd,
            HDCamera camera,
            RTHandle source,
            RTHandle destination
        )
        {
            // HDRP installs custom post-process handle properties after TAAU / DLSS. The
            // camera's original render size can differ from this post-upscale viewport.
            RTHandleProperties handles = source.rtHandleProperties;
            RenderBloom(
                cmd,
                source,
                destination,
                handles.currentViewportSize.x,
                handles.currentViewportSize.y,
                handles.rtHandleScale
            );
        }

        internal void RenderBloom(
            CommandBuffer cmd,
            RTHandle source,
            RTHandle destination,
            int width,
            int height,
            Vector2 sourceScale = default
        )
        {
            int halfWidth = Mathf.Max(1, width >> 1);
            int halfHeight = Mathf.Max(1, height >> 1);
            int count = Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Log(Mathf.Max(halfWidth, halfHeight), 2f) - 1f),
                1,
                maxIterations.value
            );

            for (int mip = 0; mip < count; mip++)
            {
                EnsureTarget(
                    ref down[mip],
                    halfWidth,
                    halfHeight,
                    source,
                    "BROcoli Bloom Down " + mip
                );
                EnsureTarget(ref up[mip], halfWidth, halfHeight, source, "BROcoli Bloom Up " + mip);
                halfWidth = Mathf.Max(1, halfWidth >> 1);
                halfHeight = Mathf.Max(1, halfHeight >> 1);
            }

            Draw(cmd, source, down[0], 0, width, height, sourceScale: sourceScale);
            for (int mip = 1; mip < count; mip++)
            {
                Draw(cmd, down[mip - 1], up[mip], 1);
                Draw(cmd, up[mip], down[mip], 2);
            }
            RTHandle bloom = down[count - 1];
            for (int mip = count - 2; mip >= 0; mip--)
            {
                Draw(cmd, down[mip], up[mip], 3, low: bloom);
                bloom = up[mip];
            }
            Draw(cmd, source, destination, 4, width, height, bloom, sourceScale);
        }

        private static void EnsureTarget(
            ref RTHandle target,
            int width,
            int height,
            RTHandle source,
            string name
        )
        {
            if (
                target != null
                && target.rt.width == width
                && target.rt.height == height
                && target.rt.dimension == source.rt.dimension
                && target.rt.volumeDepth == source.rt.volumeDepth
            )
                return;
            target?.Release();
            target = RTHandles.Alloc(
                width,
                height,
                slices: source.rt.volumeDepth,
                dimension: source.rt.dimension,
                colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                filterMode: FilterMode.Bilinear,
                wrapMode: TextureWrapMode.Clamp,
                name: name
            );
        }

        private void Draw(
            CommandBuffer cmd,
            RTHandle source,
            RTHandle destination,
            int pass,
            int viewportWidth = 0,
            int viewportHeight = 0,
            RTHandle low = null,
            Vector2 sourceScale = default
        )
        {
            int width = viewportWidth > 0 ? viewportWidth : source.rt.width;
            int height = viewportHeight > 0 ? viewportHeight : source.rt.height;
            properties.Clear();
            properties.SetTexture(SourceId, source.rt);
            properties.SetVector(SourceSizeId, new Vector4(1f / width, 1f / height, width, height));
            // Camera RTHandles can be larger than their current viewport. Clamp within the
            // valid rectangle before scaling, so resize / dynamic resolution never samples stale pixels.
            properties.SetVector(
                SourceScaleId,
                new Vector4(
                    sourceScale.x > 0f ? sourceScale.x : (float)width / source.rt.width,
                    sourceScale.y > 0f ? sourceScale.y : (float)height / source.rt.height,
                    0f,
                    0f
                )
            );
            float linearThreshold = Mathf.GammaToLinearSpace(threshold.value);
            properties.SetVector(
                ParamsId,
                new Vector4(
                    linearThreshold,
                    linearThreshold * 0.5f,
                    Mathf.Lerp(0.05f, 0.95f, scatter.value),
                    intensity.value
                )
            );
            if (low != null)
            {
                properties.SetTexture(LowId, low.rt);
                properties.SetVector(
                    LowSizeId,
                    new Vector4(low.rt.width, low.rt.height, 1f / low.rt.width, 1f / low.rt.height)
                );
            }
            // Unity copies the property block into each draw command. Mutating one material
            // repeatedly while recording the pyramid would make every pass see the final values.
            CoreUtils.SetRenderTarget(cmd, destination);
            if (pass == 4)
                cmd.SetViewport(new Rect(0f, 0f, width, height));
            CoreUtils.DrawFullScreen(cmd, material, properties, pass);
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(material);
            material = null;
            for (int mip = 0; mip < MaximumMips; mip++)
            {
                down[mip]?.Release();
                up[mip]?.Release();
                down[mip] = null;
                up[mip] = null;
            }
        }
    }
}
