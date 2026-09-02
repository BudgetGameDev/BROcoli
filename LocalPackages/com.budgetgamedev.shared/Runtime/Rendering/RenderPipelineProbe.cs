using System;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// Names the active render pipeline without referencing either pipeline's assembly.
    /// The shared runtime is linked into both builds, so it may not mention a type that
    /// only one of them ships; the pipeline asset's own type name is the one signal that
    /// is available to both.
    /// </summary>
    public static class RenderPipelineProbe
    {
        private const string UniversalAssetType =
            "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset";
        private const string HighDefinitionAssetType =
            "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset";

        /// <summary>The pipeline the current graphics settings select.</summary>
        public static RenderPipelineKind Current => Classify(CurrentAssetTypeName());

        /// <summary>True when the High Definition front end owns this build's rendering.</summary>
        public static bool IsHighDefinition => Current == RenderPipelineKind.HighDefinition;

        /// <summary>True when the Universal front end owns this build's rendering.</summary>
        public static bool IsUniversal => Current == RenderPipelineKind.Universal;

        /// <summary>
        /// Maps a render pipeline asset's type name onto the pipeline it belongs to. Both
        /// pipelines subclass their asset type for quality tiers, so the base name is
        /// matched as a prefix of the inheritance chain rather than compared outright.
        /// </summary>
        internal static RenderPipelineKind Classify(string assetTypeName)
        {
            if (string.IsNullOrEmpty(assetTypeName))
                return RenderPipelineKind.Unknown;
            if (assetTypeName.StartsWith(UniversalAssetType, StringComparison.Ordinal))
                return RenderPipelineKind.Universal;
            if (assetTypeName.StartsWith(HighDefinitionAssetType, StringComparison.Ordinal))
                return RenderPipelineKind.HighDefinition;
            return RenderPipelineKind.Unknown;
        }

        private static string CurrentAssetTypeName()
        {
            RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null)
                return null;

            for (Type type = asset.GetType(); type != null; type = type.BaseType)
            {
                RenderPipelineKind kind = Classify(type.FullName);
                if (kind != RenderPipelineKind.Unknown)
                    return type.FullName;
            }

            return asset.GetType().FullName;
        }
    }
}
