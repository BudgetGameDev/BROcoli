using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Recreates ignored Unity assets from licensed encrypted sources.</summary>
[InitializeOnLoad]
public sealed partial class LicensedAssetDecryptor : IPreprocessBuildWithReport
{
    private const string KeyName = "BROCOLI_LICENSED_ASSET_KEY";
    private const string EncryptedRoot = "Assets/Encrypted/Licensed";
    private const string LegacyGeneratedRoot = "Assets/Resources/Generated/Licensed";
    private const string PackageGeneratedRoot = "Assets/Generated/Licensed";
    private const string PackageMarkerName = ".brocoli-package.sha256";
    private const int Iterations = 200000;
    private const int PackageFormatVersion = 2;
    private const int MaximumPackageFiles = 200000;
    private const long MaximumPackageBytes = 20L * 1024 * 1024 * 1024;
    private static readonly byte[] OpenSslMagic = Encoding.ASCII.GetBytes("Salted__");
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

    [Serializable]
    private sealed class AssetMetadata
    {
        public int formatVersion;
        public string payloadType;
        public string archiveFormat;
        public string generatedPath;
        public string rootGuid;
        public string sha256;
        public int fileCount;
        public long uncompressedSize;
        public string title;
        public string sourceUrl;
        public string author;
        public string license;
        public string assetVersion;
        public string licenseType;
        public string acquiredDate;
        public string price;
    }

    static LicensedAssetDecryptor()
    {
        EditorApplication.delayCall += () => DecryptAll(false);
    }

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!DecryptAll(true))
            throw new BuildFailedException(
                $"Licensed assets require {KeyName}. Copy .env.example to .env and add the repository key."
            );
    }

    [MenuItem("BROcoli/Licensed Assets/Decrypt All")]
    private static void DecryptFromMenu()
    {
        if (DecryptAll(true))
            Debug.Log("Licensed assets are ready.");
    }

    private static bool DecryptAll(bool logMissingKey)
    {
        string encryptedRoot = ProjectPath(EncryptedRoot);
        if (!Directory.Exists(encryptedRoot))
            return true;

        string[] encryptedFiles = Directory.GetFiles(
            encryptedRoot,
            "*.enc",
            SearchOption.TopDirectoryOnly
        );
        if (encryptedFiles.Length == 0)
            return true;

        string secret = ReadSecret();
        if (string.IsNullOrWhiteSpace(secret))
        {
            if (logMissingKey)
                Debug.LogError($"Missing {KeyName}; licensed assets cannot be generated.");
            return false;
        }

        bool changed = false;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (
                string encryptedPath in encryptedFiles.OrderBy(path => path, StringComparer.Ordinal)
            )
                changed |= DecryptAsset(encryptedPath, secret);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        if (changed)
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return true;
    }

    private static bool DecryptAsset(string encryptedPath, string secret)
    {
        string metadataPath = encryptedPath + ".json";
        if (!File.Exists(metadataPath))
            throw new InvalidDataException($"Missing licensed asset metadata: {metadataPath}");

        AssetMetadata metadata = JsonUtility.FromJson<AssetMetadata>(
            File.ReadAllText(metadataPath)
        );
        ValidateMetadata(metadata, metadataPath);
        if (IsCurrent(metadata))
            return false;

        string payloadPath = DecryptToTemporaryFile(encryptedPath, secret);
        try
        {
            if (!HashFile(payloadPath).Equals(metadata.sha256, StringComparison.OrdinalIgnoreCase))
                throw new CryptographicException(
                    $"Wrong key or damaged licensed asset: {encryptedPath}"
                );

            if (metadata.formatVersion == 1)
                RestoreFile(payloadPath, metadata);
            else
                RestorePackage(payloadPath, metadata);
        }
        finally
        {
            File.Delete(payloadPath);
        }

        string label = string.IsNullOrWhiteSpace(metadata.title) ? metadata.author : metadata.title;
        Debug.Log($"Generated licensed asset for {label}: {metadata.generatedPath}");
        return true;
    }

    private static bool IsCurrent(AssetMetadata metadata)
    {
        string generatedPath = ProjectPath(metadata.generatedPath);
        if (metadata.formatVersion == 1)
        {
            return File.Exists(generatedPath)
                && HashFile(generatedPath)
                    .Equals(metadata.sha256, StringComparison.OrdinalIgnoreCase);
        }

        string markerPath = Path.Combine(generatedPath, PackageMarkerName);
        string rootMetaPath = generatedPath.TrimEnd(Path.DirectorySeparatorChar) + ".meta";
        return Directory.Exists(generatedPath)
            && File.Exists(markerPath)
            && File.ReadAllText(markerPath)
                .Trim()
                .Equals(metadata.sha256, StringComparison.OrdinalIgnoreCase)
            && File.Exists(rootMetaPath)
            && File.ReadAllText(rootMetaPath).Contains($"guid: {metadata.rootGuid}");
    }
}
