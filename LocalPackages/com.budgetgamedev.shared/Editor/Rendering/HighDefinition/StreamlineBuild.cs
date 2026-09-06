using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BudgetGameDev.Hub.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace BudgetGameDev.Shared.Rendering.HighDefinition.Editor
{
    public sealed class StreamlineBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string Package = "Packages/com.budgetgamedev.shared";
        private const string PluginName = "GfxPluginBudgetGameDevStreamline.dll";
        private const string PluginPath = Package + "/Runtime/Plugins/Streamline/" + PluginName;
        public int callbackOrder => int.MinValue + 1;

        [Serializable]
        private sealed class Entry
        {
            public string name;
            public string sha256;
        }

        [Serializable]
        private sealed class Manifest
        {
            public string sdkVersion;
            public Entry[] files;
        }

        private static string NativeRoot =>
            Path.Combine(
                UnityEditor
                    .PackageManager.PackageInfo.FindForAssembly(typeof(StreamlineBuild).Assembly)
                    .resolvedPath,
                "Native~/Streamline"
            );

        private static bool Applies(BuildTarget target) =>
            target == BuildTarget.StandaloneWindows64
            && BuildRenderingPolicy.PipelineFor(target) == RenderPipelineKind.HighDefinition;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!Applies(report.summary.platform))
                return;
            ValidatePayload();
            var hook = typeof(HDRenderPipeline).Assembly.GetType(
                "UnityEngine.Rendering.HighDefinition.SharedFrameGenerationHooks"
            );
            if (hook?.GetField("Version")?.GetRawConstantValue() is not int version || version != 1)
                throw new BuildFailedException(
                    "Streamline needs the shared HDRP 17.5 final-color hook. Run Tools~/Streamline/setup.py and let Unity recompile."
                );
            var importer = AssetImporter.GetAtPath(PluginPath) as PluginImporter;
            if (
                importer == null
                || !importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64)
                || importer.GetCompatibleWithEditor()
                || !importer.isPreloaded
            )
                throw new BuildFailedException(
                    "Streamline's native plugin must be imported for Windows x64, Editor disabled, and preloaded. Refresh Unity after setup."
                );
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D12 }
            );
        }

        public static void ValidatePayload()
        {
            var manifest = JsonUtility.FromJson<Manifest>(
                File.ReadAllText(Path.Combine(NativeRoot, "production.json"))
            );
            var directory = Path.Combine(NativeRoot, "artifacts/win-x64");
            if (!File.Exists(Path.Combine(directory, PluginName)))
                throw new BuildFailedException(
                    "Streamline Windows bridge is missing. Run the shared package's Tools~/Streamline/setup.py."
                );
            ValidateX64(Path.Combine(directory, PluginName));
            foreach (var entry in manifest.files)
            {
                string path = Path.Combine(directory, entry.name);
                if (!File.Exists(path) || Hash(path) != entry.sha256)
                    throw new BuildFailedException(
                        $"Missing or modified Streamline {manifest.sdkVersion} production payload: {entry.name}. Rerun shared setup."
                    );
                if (entry.name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    ValidateX64(path);
            }
            var plugin =
                UnityEditor
                    .PackageManager.PackageInfo.FindForAssembly(typeof(StreamlineBuild).Assembly)
                    .resolvedPath
                + "/Runtime/Plugins/Streamline/"
                + PluginName;
            if (!File.Exists(plugin) || Hash(plugin) != Hash(Path.Combine(directory, PluginName)))
                throw new BuildFailedException(
                    "The imported Streamline bridge differs from the prepared native build."
                );
        }

        private static string Hash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static void ValidateX64(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.BaseStream.Length < 64 || reader.ReadUInt16() != 0x5a4d)
                throw new BuildFailedException($"Invalid Windows DLL: {path}");
            reader.BaseStream.Position = 0x3c;
            uint offset = reader.ReadUInt32();
            if (offset > reader.BaseStream.Length - 6)
                throw new BuildFailedException($"Invalid PE header: {path}");
            reader.BaseStream.Position = offset;
            if (reader.ReadUInt32() != 0x4550 || reader.ReadUInt16() != 0x8664)
                throw new BuildFailedException($"Streamline requires x64 PE binaries: {path}");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!Applies(report.summary.platform))
                return;
            string output = Path.GetDirectoryName(report.summary.outputPath);
            var manifest = JsonUtility.FromJson<Manifest>(
                File.ReadAllText(Path.Combine(NativeRoot, "production.json"))
            );
            var directory = Path.Combine(NativeRoot, "artifacts/win-x64");
            foreach (var entry in manifest.files)
                File.Copy(
                    Path.Combine(directory, entry.name),
                    Path.Combine(output, entry.name),
                    true
                );
            File.Copy(
                Path.Combine(directory, "streamline.license.txt"),
                Path.Combine(output, "streamline.license.txt"),
                true
            );
        }
    }

    internal sealed class StreamlinePluginImporter : AssetPostprocessor
    {
        private void OnPreprocessAsset()
        {
            if (
                !assetPath.EndsWith(
                    "/Streamline/GfxPluginBudgetGameDevStreamline.dll",
                    StringComparison.Ordinal
                )
            )
                return;
            if (assetImporter is not PluginImporter plugin)
                return;
            plugin.SetCompatibleWithAnyPlatform(false);
            plugin.SetCompatibleWithEditor(false);
            plugin.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
            plugin.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");
            plugin.isPreloaded = true;
            plugin.DefineConstraints = new[] { "!BROCOLI_URP_PLAYER" };
        }
    }
}
