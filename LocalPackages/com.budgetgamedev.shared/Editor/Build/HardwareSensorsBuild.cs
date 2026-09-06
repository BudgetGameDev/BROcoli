using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BudgetGameDev.Shared.Editor
{
    /// <summary>Ship the isolated sensor runtime as data, never as Unity plugins.</summary>
    public sealed class HardwareSensorsBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;
        private static string Payload => Path.Combine(
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(HardwareSensorsBuild).Assembly).resolvedPath,
            "Native~/HardwareSensors/artifacts/win-x64");

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            foreach (string file in new[] { "HardwareSensors.exe", "HardwareSensors.runtimeconfig.json", "THIRD-PARTY-NOTICES.txt" })
                if (!File.Exists(Path.Combine(Payload, file)))
                    throw new BuildFailedException("Hardware sensor payload missing. Run python scripts/build-hardware-sensors.py before building Windows.");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            string destination = Path.Combine(Path.GetDirectoryName(report.summary.outputPath),
                Path.GetFileNameWithoutExtension(report.summary.outputPath) + "_Data", "StreamingAssets", "HardwareSensors");
            foreach (string source in Directory.GetFiles(Payload, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destination, Path.GetRelativePath(Payload, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, true);
            }
        }
    }
}
