using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace BudgetGameDev.Hub.Editor
{
    /// <summary>Release content is chosen before Unity opens the isolated project.</summary>
    public static class BuildContentPolicy
    {
        public const string StageFile = "BuildContent.json";

        [Serializable]
        public sealed class Selection
        {
            public string product;
            public string productName;
            public string[] gamePackages;
            public string[] localPackages;
            public string[] excludedAssemblies;
            public bool development;
        }

        public static Selection Current =>
            File.Exists(StageFile)
                ? JsonUtility.FromJson<Selection>(File.ReadAllText(StageFile))
                : null;

        public static string ProductName => Current?.productName ?? "BROcoli";
        public static bool IncludesLauncher => Current == null || Current.product == "launcher";

        public static void Validate(bool development)
        {
            Selection selection = Current;
            if (selection == null)
            {
                if (!development)
                    throw new BuildFailedException(
                        "Release builds require an isolated project. Run scripts/release-build.py "
                            + "--product brocoli (or --product launcher). Selecting scenes cannot exclude code or Resources."
                    );
                return;
            }
            if (selection.development != development)
                throw new BuildFailedException(
                    "Build development mode differs from the staged content selection."
                );
            if (
                selection.gamePackages == null
                || selection.gamePackages.Length == 0
                || selection.localPackages == null
                || string.IsNullOrEmpty(selection.product)
            )
                throw new BuildFailedException("Invalid staged build content selection.");
            var installed = PackageInfo
                .GetAllRegisteredPackages()
                .Where(package =>
                    package.name.StartsWith("com.budgetgamedev.", StringComparison.Ordinal)
                )
                .Select(package => package.name)
                .OrderBy(name => name)
                .ToArray();
            if (!installed.SequenceEqual(selection.localPackages.OrderBy(name => name)))
                throw new BuildFailedException(
                    "Imported package set differs from the staged allowlist: "
                        + string.Join(", ", installed)
                );
            if (
                !development
                && installed.Any(name =>
                    name.StartsWith("com.budgetgamedev.autoplay", StringComparison.Ordinal)
                )
            )
                throw new BuildFailedException("Release project imported an autoplay package.");
            var games = installed.Where(name =>
                name.StartsWith("com.budgetgamedev.game.", StringComparison.Ordinal)
            );
            if (!games.SequenceEqual(selection.gamePackages.OrderBy(name => name)))
                throw new BuildFailedException("Imported games differ from the requested product.");
            if (!IncludesLauncher && installed.Contains("com.budgetgamedev.hub"))
                throw new BuildFailedException(
                    "Single-game project imported the launcher package."
                );
        }

        public static BuildPlayerOptions PrepareOptions(BuildPlayerOptions options)
        {
            Validate((options.options & BuildOptions.Development) != 0);
            if (Current != null)
                PlayerSettings.productName = ProductName;
            return options;
        }
    }

    /// <summary>Fail closed for Build Settings, CLI and custom BuildPipeline callers alike.</summary>
    public sealed class BuildContentGate
        : BuildPlayerProcessor,
            IFilterBuildAssemblies,
            IPostprocessBuildWithReport
    {
        public override int callbackOrder => int.MinValue;

        public override void PrepareForBuild(BuildPlayerContext context) =>
            BuildContentPolicy.Validate(
                (context.BuildPlayerOptions.options & BuildOptions.Development) != 0
            );

        public string[] OnFilterAssemblies(BuildOptions options, string[] assemblies)
        {
            bool release = (options & BuildOptions.Development) == 0;
            BuildContentPolicy.Validate(!release);
            var selection = BuildContentPolicy.Current;
            foreach (string path in assemblies)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (
                    (release && name.IndexOf("Autoplay", StringComparison.OrdinalIgnoreCase) >= 0)
                    || (selection?.excludedAssemblies?.Contains(name) ?? false)
                )
                    throw new BuildFailedException(
                        $"Excluded assembly was compiled for the player: {name}"
                    );
            }
            return assemblies;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            var selection = BuildContentPolicy.Current;
            // Unity finalizes summary.result after postprocess callbacks. The
            // manifest describes selected content, not whether the build passed;
            // the final build result and warning gate remain authoritative.
            if (selection == null)
                return;
            string output = report.summary.outputPath;
            string directory = Path.HasExtension(output) ? Path.GetDirectoryName(output) : output;
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "build-content.json"),
                JsonUtility.ToJson(selection, true)
            );
        }
    }
}
