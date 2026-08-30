using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BudgetGameDev.Hub.Editor
{
    /// <summary>
    /// Mirrors the project's root <c>LauncherConfig.txt</c> into a Resources
    /// folder so a built player can read it.
    /// </summary>
    /// <remarks>
    /// The file people edit lives at the project root, beside `.env`, where it is
    /// easy to find and obviously a project-level setting. Nothing outside
    /// `Assets/` reaches a player, though, so the launcher reads a generated copy
    /// instead. The copy is ignored by git and regenerated, so the root file stays
    /// the single source of truth.
    ///
    /// Unity does not watch files outside `Assets/`, so there is no import event
    /// to hook. Syncing happens at the three moments the copy could otherwise be
    /// stale: editor load, entering play mode, and building.
    /// </remarks>
    public sealed class LauncherConfigSync : IPreprocessBuildWithReport
    {
        /// <summary>Authored file, relative to the project root.</summary>
        public const string SourcePath = "LauncherConfig.txt";

        private const string GeneratedDirectory = "Assets/Generated/Resources";

        private static readonly string GeneratedPath =
            $"{GeneratedDirectory}/{LauncherConfig.ResourceName}.txt";

        private const string Header =
            "# Generated from LauncherConfig.txt at the project root. Do not edit.\n"
            + "# Edit that file instead; this copy exists only so a built player\n"
            + "# can read it, and is regenerated on load, on play, and on build.\n";

        // Ahead of the scene-list sync so a build has both before it starts.
        public int callbackOrder => -950;

        public void OnPreprocessBuild(BuildReport report) => Sync();

        [InitializeOnLoadMethod]
        private static void SyncOnLoad()
        {
            Sync();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    Sync();
            };
        }

        [MenuItem("Budget GameDev/Sync Launcher Config")]
        private static void SyncFromMenu()
        {
            Sync();
            Debug.Log($"[Launcher] Synced {SourcePath} into {GeneratedPath}.");
        }

        /// <summary>
        /// Rewrites the generated copy when it differs from the root file. A
        /// missing root file removes the copy, which leaves the launcher on its
        /// default behaviour rather than serving a stale config forever.
        /// </summary>
        public static void Sync()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string source = Path.Combine(projectRoot, SourcePath);
            string generated = Path.Combine(projectRoot, GeneratedPath);

            if (!File.Exists(source))
            {
                if (!File.Exists(generated))
                    return;

                AssetDatabase.DeleteAsset(GeneratedPath);
                Debug.LogWarning(
                    $"[Launcher] {SourcePath} is missing; removed the generated copy."
                );
                return;
            }

            string wanted = Header + File.ReadAllText(source);
            if (File.Exists(generated) && File.ReadAllText(generated) == wanted)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(generated)!);
            File.WriteAllText(generated, wanted);
            AssetDatabase.ImportAsset(GeneratedPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
