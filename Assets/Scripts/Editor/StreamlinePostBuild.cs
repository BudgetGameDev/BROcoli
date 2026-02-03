// StreamlinePostBuild.cs - Post-build processor for NVIDIA Streamline/DLSS
// 
// This script copies NGX DLLs (nvngx_dlss.dll, nvngx_dlssg.dll) from the Plugins folder
// to the same directory as the built executable. NGX runtime requires these DLLs to be
// in the application directory, not in a subdirectory like Unity's Plugins folder.
//
// It also copies NGXCore DLLs (_nvngx.dll, nvngx.dll) from the NVIDIA driver store
// so the game doesn't have to hunt for them at runtime.
//
// Without this, DLSS will fail with: "nvngx_dlss.dll doesn't exist in any of the search paths!"

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Linq;

/// <summary>
/// Post-build processor that copies NGX DLLs to the executable directory for DLSS support.
/// NGX runtime searches for nvngx_*.dll in the application directory, not Unity's Plugins subfolder.
/// Also copies NGXCore from NVIDIA driver store for self-contained deployment.
/// </summary>
public class StreamlinePostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 100; // Run after other post-processors

    // NGX DLLs required for DLSS and Frame Generation
    private static readonly string[] NgxDlls = new string[]
    {
        "nvngx_dlss.dll",       // DLSS Super Resolution neural network
        "nvngx_dlssg.dll",      // DLSS Frame Generation neural network
        "nvngx_dlssd.dll",      // DLSS Denoiser (Ray Reconstruction)
    };

    // Streamline DLLs that should also be next to exe for easier discovery
    private static readonly string[] StreamlineDlls = new string[]
    {
        "sl.interposer.dll",
        "sl.common.dll",
        "sl.dlss.dll",
        "sl.dlss_g.dll",
        "sl.reflex.dll",
        "sl.pcl.dll",
    };

    // NGXCore DLLs to copy from driver store (for self-contained deployment)
    private static readonly string[] NgxCoreDlls = new string[]
    {
        "_nvngx.dll",           // NGXCore primary
        "nvngx.dll",            // NGXCore secondary
    };

    public void OnPostprocessBuild(BuildReport report)
    {
        // Only process Windows Standalone builds
        if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
            report.summary.platform != BuildTarget.StandaloneWindows)
        {
            return;
        }

        string outputPath = report.summary.outputPath;
        string outputDir = Path.GetDirectoryName(outputPath);
        string exeName = Path.GetFileNameWithoutExtension(outputPath);
        
        // Unity places plugins in {ExeName}_Data/Plugins/x86_64/
        string pluginsDir = Path.Combine(outputDir, $"{exeName}_Data", "Plugins", "x86_64");
        
        Debug.Log($"[StreamlinePostBuild] Output directory: {outputDir}");
        Debug.Log($"[StreamlinePostBuild] Plugins directory: {pluginsDir}");

        if (!Directory.Exists(pluginsDir))
        {
            Debug.LogWarning($"[StreamlinePostBuild] Plugins directory not found: {pluginsDir}");
            return;
        }

        int copiedCount = 0;

        // Copy NGX DLLs to exe directory (required for NGX to find them)
        foreach (string dll in NgxDlls)
        {
            string srcPath = Path.Combine(pluginsDir, dll);
            string dstPath = Path.Combine(outputDir, dll);

            if (File.Exists(srcPath))
            {
                try
                {
                    File.Copy(srcPath, dstPath, overwrite: true);
                    Debug.Log($"[StreamlinePostBuild] Copied {dll} to exe directory");
                    copiedCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[StreamlinePostBuild] Failed to copy {dll}: {e.Message}");
                }
            }
            else
            {
                Debug.Log($"[StreamlinePostBuild] Optional DLL not found: {dll}");
            }
        }

        // Copy Streamline DLLs to exe directory for better compatibility
        foreach (string dll in StreamlineDlls)
        {
            string srcPath = Path.Combine(pluginsDir, dll);
            string dstPath = Path.Combine(outputDir, dll);

            if (File.Exists(srcPath))
            {
                try
                {
                    File.Copy(srcPath, dstPath, overwrite: true);
                    Debug.Log($"[StreamlinePostBuild] Copied {dll} to exe directory");
                    copiedCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[StreamlinePostBuild] Failed to copy {dll}: {e.Message}");
                }
            }
        }

        if (copiedCount > 0)
        {
            Debug.Log($"[StreamlinePostBuild] Copied {copiedCount} Streamline/NGX DLLs to {outputDir}");
        }
        else
        {
            Debug.LogWarning("[StreamlinePostBuild] No Streamline/NGX DLLs found to copy. " +
                           "DLSS may not function. Ensure DLLs are in Assets/Plugins/x86_64/");
        }

        // Copy NGXCore DLLs from NVIDIA driver store for self-contained deployment
        CopyNgxCoreFromDriverStore(outputDir);
    }

    /// <summary>
    /// Finds and copies NGXCore DLLs (_nvngx.dll, nvngx.dll) from the NVIDIA driver store.
    /// This makes the build self-contained so it doesn't need to search the driver store at runtime.
    /// </summary>
    private void CopyNgxCoreFromDriverStore(string outputDir)
    {
        // NGXCore lives in the NVIDIA driver store under System32\DriverStore\FileRepository
        string driverStoreRoot = @"C:\Windows\System32\DriverStore\FileRepository";
        
        if (!Directory.Exists(driverStoreRoot))
        {
            Debug.LogWarning("[StreamlinePostBuild] Driver store not found. NGXCore will load from system path.");
            return;
        }

        // Find NVIDIA display driver folders (nv_dispi.inf_amd64_*)
        string[] nvDriverDirs;
        try
        {
            nvDriverDirs = Directory.GetDirectories(driverStoreRoot, "nv_dispi.inf_amd64_*");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[StreamlinePostBuild] Could not enumerate driver store: {e.Message}");
            return;
        }

        if (nvDriverDirs.Length == 0)
        {
            Debug.LogWarning("[StreamlinePostBuild] No NVIDIA driver folders found in driver store.");
            return;
        }

        // Sort by last write time to get the most recent driver
        var sortedDirs = nvDriverDirs
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(d => d.LastWriteTime)
            .ToArray();

        Debug.Log($"[StreamlinePostBuild] Found {sortedDirs.Length} NVIDIA driver folders, using newest: {sortedDirs[0].Name}");

        string newestDriverDir = sortedDirs[0].FullName;
        int ngxCoreCopied = 0;

        foreach (string dll in NgxCoreDlls)
        {
            string srcPath = Path.Combine(newestDriverDir, dll);
            string dstPath = Path.Combine(outputDir, dll);

            if (File.Exists(srcPath))
            {
                try
                {
                    File.Copy(srcPath, dstPath, overwrite: true);
                    var fileInfo = new FileInfo(srcPath);
                    Debug.Log($"[StreamlinePostBuild] Copied NGXCore {dll} ({fileInfo.Length / 1024}KB) from driver store");
                    ngxCoreCopied++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[StreamlinePostBuild] Failed to copy NGXCore {dll}: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[StreamlinePostBuild] NGXCore {dll} not found in {newestDriverDir}");
            }
        }

        if (ngxCoreCopied > 0)
        {
            Debug.Log($"[StreamlinePostBuild] Copied {ngxCoreCopied} NGXCore DLLs for self-contained deployment");
        }
    }
}
#endif
