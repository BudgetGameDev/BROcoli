using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;
using UnityEngine;

/// <summary>
/// Post-build processor that configures Player.log to be written next to the executable
/// instead of the default AppData/LocalLow location. This makes it easier for players
/// to submit logs for debugging.
/// </summary>
public class ConfigurePlayerLog : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        // Only process standalone Windows builds
        if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
            report.summary.platform != BuildTarget.StandaloneWindows)
        {
            return;
        }

        string outputPath = report.summary.outputPath;
        string outputDir = Path.GetDirectoryName(outputPath);
        string exeName = Path.GetFileNameWithoutExtension(outputPath);
        string dataFolder = Path.Combine(outputDir, exeName + "_Data");
        string bootConfigPath = Path.Combine(dataFolder, "boot.config");

        if (!File.Exists(bootConfigPath))
        {
            Debug.LogWarning($"[ConfigurePlayerLog] boot.config not found at: {bootConfigPath}");
            return;
        }

        // Read existing boot.config
        string content = File.ReadAllText(bootConfigPath);

        // Add player-log-file setting if not already present
        // Using "..\\" to go up from Data folder to exe folder, then Player.log
        // This ensures the log file is written next to the .exe
        if (!content.Contains("player-log-file="))
        {
            content += "\nplayer-log-file=..\\Player.log";
            File.WriteAllText(bootConfigPath, content);
            Debug.Log($"[ConfigurePlayerLog] Added player-log-file setting to boot.config: ..\\Player.log");
        }
        else if (!content.Contains("..\\Player.log") && !content.Contains("../Player.log"))
        {
            // Update existing setting if it's not using the correct relative path
            content = System.Text.RegularExpressions.Regex.Replace(
                content, 
                @"player-log-file=.*", 
                "player-log-file=..\\Player.log");
            File.WriteAllText(bootConfigPath, content);
            Debug.Log($"[ConfigurePlayerLog] Updated player-log-file setting to: ..\\Player.log");
        }
    }
}
