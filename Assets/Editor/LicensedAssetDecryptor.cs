using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Recreates ignored Unity assets from licensed encrypted sources.</summary>
[InitializeOnLoad]
public sealed class LicensedAssetDecryptor : IPreprocessBuildWithReport
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
                string encryptedPath in encryptedFiles.OrderBy(
                    path => path,
                    StringComparer.Ordinal
                )
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
                && HashFile(generatedPath).Equals(
                    metadata.sha256,
                    StringComparison.OrdinalIgnoreCase
                );
        }

        string markerPath = Path.Combine(generatedPath, PackageMarkerName);
        string rootMetaPath = generatedPath.TrimEnd(Path.DirectorySeparatorChar) + ".meta";
        return Directory.Exists(generatedPath)
            && File.Exists(markerPath)
            && File.ReadAllText(markerPath).Trim().Equals(
                metadata.sha256,
                StringComparison.OrdinalIgnoreCase
            )
            && File.Exists(rootMetaPath)
            && File.ReadAllText(rootMetaPath).Contains($"guid: {metadata.rootGuid}");
    }

    private static void RestoreFile(string payloadPath, AssetMetadata metadata)
    {
        string targetPath = ProjectPath(metadata.generatedPath);
        string directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string stagingPath = targetPath + ".brocoli-staging-" + Guid.NewGuid().ToString("N");
        string backupPath = targetPath + ".brocoli-backup-" + Guid.NewGuid().ToString("N");
        bool targetReplaced = false;
        try
        {
            File.Copy(payloadPath, stagingPath, false);
            if (File.Exists(targetPath))
                File.Move(targetPath, backupPath);
            try
            {
                File.Move(stagingPath, targetPath);
                targetReplaced = true;
            }
            catch
            {
                if (File.Exists(backupPath) && !File.Exists(targetPath))
                    File.Move(backupPath, targetPath);
                throw;
            }
            File.Delete(backupPath);
        }
        finally
        {
            File.Delete(stagingPath);
            if (targetReplaced)
                File.Delete(backupPath);
        }
    }

    private static void RestorePackage(string payloadPath, AssetMetadata metadata)
    {
        string targetPath = ProjectPath(metadata.generatedPath);
        string parentPath = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrEmpty(parentPath))
            throw new InvalidDataException("Licensed package target has no parent directory.");
        Directory.CreateDirectory(parentPath);

        string stagingPath = Path.Combine(
            parentPath,
            ".brocoli-staging-" + Guid.NewGuid().ToString("N")
        );
        string backupPath = Path.Combine(
            parentPath,
            ".brocoli-backup-" + Guid.NewGuid().ToString("N")
        );
        bool targetReplaced = false;
        try
        {
            Directory.CreateDirectory(stagingPath);
            ExtractPackage(payloadPath, stagingPath, metadata);
            File.WriteAllText(
                Path.Combine(stagingPath, PackageMarkerName),
                metadata.sha256 + "\n",
                Utf8WithoutBom
            );

            if (Directory.Exists(targetPath))
                Directory.Move(targetPath, backupPath);
            try
            {
                Directory.Move(stagingPath, targetPath);
                targetReplaced = true;
            }
            catch
            {
                if (Directory.Exists(backupPath) && !Directory.Exists(targetPath))
                    Directory.Move(backupPath, targetPath);
                throw;
            }

            DeleteDirectoryIfPresent(backupPath);
            WritePackageRootMeta(targetPath, metadata.rootGuid);
        }
        finally
        {
            DeleteDirectoryIfPresent(stagingPath);
            if (targetReplaced)
                DeleteDirectoryIfPresent(backupPath);
        }
    }

    private static void ExtractPackage(
        string payloadPath,
        string stagingPath,
        AssetMetadata metadata
    )
    {
        using ZipArchive archive = ZipFile.OpenRead(payloadPath);
        var names = new HashSet<string>(StringComparer.Ordinal);
        int fileCount = 0;
        long uncompressedSize = 0;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string relativePath = ValidateArchiveEntry(entry);
            if (!names.Add(relativePath))
                throw new InvalidDataException(
                    $"Duplicate licensed package archive entry: {relativePath}"
                );
            if (IsDirectory(entry))
                continue;
            fileCount++;
            uncompressedSize = checked(uncompressedSize + entry.Length);
            if (fileCount > MaximumPackageFiles || uncompressedSize > MaximumPackageBytes)
                throw new InvalidDataException("Licensed package exceeds extraction safety limits.");
        }

        if (fileCount != metadata.fileCount || uncompressedSize != metadata.uncompressedSize)
            throw new InvalidDataException(
                "Licensed package archive contents do not match its metadata."
            );

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string relativePath = ValidateArchiveEntry(entry);
            string destination = SafeArchiveDestination(stagingPath, relativePath);
            if (IsDirectory(entry))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            string directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            using Stream source = entry.Open();
            using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
            source.CopyTo(target);
        }
    }

    private static string ValidateArchiveEntry(ZipArchiveEntry entry)
    {
        string name = entry.FullName;
        if (
            string.IsNullOrWhiteSpace(name)
            || name.Contains('\\')
            || name.StartsWith("/", StringComparison.Ordinal)
            || name.Contains('\0')
        )
            throw new InvalidDataException($"Unsafe licensed package archive entry: {name}");

        string normalized = name.TrimEnd('/');
        string[] parts = normalized.Split('/');
        if (
            parts.Length == 0
            || parts.Any(
                part => string.IsNullOrWhiteSpace(part)
                    || part == "."
                    || part == ".."
                    || part.Contains(':')
                    || part.Any(char.IsControl)
            )
        )
            throw new InvalidDataException($"Unsafe licensed package archive entry: {name}");

        uint externalAttributes = unchecked((uint)entry.ExternalAttributes);
        uint unixType = (externalAttributes >> 16) & 0xF000;
        if (unixType == 0xA000)
            throw new InvalidDataException(
                $"Licensed package archive cannot contain symlinks: {name}"
            );
        if (unixType != 0 && unixType != 0x4000 && unixType != 0x8000)
            throw new InvalidDataException(
                $"Licensed package archive contains an unsupported entry: {name}"
            );
        if (IsDirectory(entry) && unixType == 0x8000)
            throw new InvalidDataException(
                $"Licensed package archive has an invalid directory entry: {name}"
            );
        if (!IsDirectory(entry) && unixType == 0x4000)
            throw new InvalidDataException(
                $"Licensed package archive has an invalid file entry: {name}"
            );
        return string.Join("/", parts);
    }

    private static bool IsDirectory(ZipArchiveEntry entry)
    {
        return entry.FullName.EndsWith("/", StringComparison.Ordinal);
    }

    private static string SafeArchiveDestination(string stagingPath, string relativePath)
    {
        string root = Path.GetFullPath(stagingPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string destination = Path.GetFullPath(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))
        );
        StringComparison comparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!destination.StartsWith(root + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException(
                $"Licensed package archive entry escapes its target: {relativePath}"
            );
        return destination;
    }

    private static string DecryptToTemporaryFile(string encryptedPath, string secret)
    {
        string temporaryRoot = ProjectPath("Library/BROcoli/LicensedAssets");
        Directory.CreateDirectory(temporaryRoot);
        string outputPath = Path.Combine(
            temporaryRoot,
            Guid.NewGuid().ToString("N") + ".payload"
        );

        try
        {
            using var input = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read);
            byte[] header = new byte[16];
            ReadExactly(input, header);
            if (!header.Take(8).SequenceEqual(OpenSslMagic))
                throw new InvalidDataException(
                    "Encrypted asset does not use the expected OpenSSL format."
                );

            byte[] salt = header.Skip(8).Take(8).ToArray();
            using var derivation = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(secret),
                salt,
                Iterations,
                HashAlgorithmName.SHA256
            );
            using Aes aes = Aes.Create();
            aes.Key = derivation.GetBytes(32);
            aes.IV = derivation.GetBytes(16);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            using var crypto = new CryptoStream(input, decryptor, CryptoStreamMode.Read);
            using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write);
            crypto.CopyTo(output);
            return outputPath;
        }
        catch
        {
            File.Delete(outputPath);
            throw;
        }
    }

    private static void ReadExactly(Stream input, byte[] buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = input.Read(buffer, offset, buffer.Length - offset);
            if (read == 0)
                throw new InvalidDataException("Encrypted asset is truncated.");
            offset += read;
        }
    }

    private static void ValidateMetadata(AssetMetadata metadata, string metadataPath)
    {
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.sha256))
            throw new InvalidDataException($"Invalid licensed asset metadata: {metadataPath}");
        if (
            metadata.sha256.Length != 64
            || metadata.sha256.Any(character => !Uri.IsHexDigit(character))
        )
            throw new InvalidDataException($"Invalid licensed asset digest: {metadataPath}");

        if (metadata.formatVersion == 1)
        {
            ValidateGeneratedPath(metadata.generatedPath, LegacyGeneratedRoot, metadataPath);
            return;
        }

        if (
            metadata.formatVersion != PackageFormatVersion
            || metadata.payloadType != "directory"
            || metadata.archiveFormat != "zip"
            || metadata.fileCount < 0
            || metadata.uncompressedSize < 0
            || metadata.fileCount > MaximumPackageFiles
            || metadata.uncompressedSize > MaximumPackageBytes
            || metadata.rootGuid == null
            || metadata.rootGuid.Length != 32
            || metadata.rootGuid.Any(character => !Uri.IsHexDigit(character))
        )
            throw new InvalidDataException($"Invalid licensed package metadata: {metadataPath}");
        ValidateGeneratedPath(metadata.generatedPath, PackageGeneratedRoot, metadataPath);
    }

    private static void ValidateGeneratedPath(
        string generatedPath,
        string requiredRoot,
        string metadataPath
    )
    {
        string normalized = NormalizeProjectRelativePath(generatedPath);
        if (!normalized.StartsWith(requiredRoot + "/", StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Generated path in {metadataPath} must stay under {requiredRoot}/"
            );
    }

    private static string NormalizeProjectRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
            throw new InvalidDataException("Generated asset path must be project-relative.");
        string normalized = value.Replace('\\', '/').Trim('/');
        string[] parts = normalized.Split('/');
        if (
            parts.Length == 0
            || parts.Any(
                part => string.IsNullOrWhiteSpace(part)
                    || part == "."
                    || part == ".."
                    || part.Contains(':')
                    || part.Any(char.IsControl)
            )
        )
            throw new InvalidDataException("Generated asset path is unsafe.");
        return string.Join("/", parts);
    }

    private static string ProjectPath(string relativePath)
    {
        string normalized = NormalizeProjectRelativePath(relativePath);
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string result = Path.GetFullPath(
            Path.Combine(projectRoot, normalized.Replace('/', Path.DirectorySeparatorChar))
        );
        StringComparison comparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!result.StartsWith(projectRoot + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException($"Project path escapes the project: {relativePath}");
        return result;
    }

    private static void WritePackageRootMeta(string packagePath, string guid)
    {
        string content =
            "fileFormatVersion: 2\n"
            + $"guid: {guid}\n"
            + "folderAsset: yes\n"
            + "DefaultImporter:\n"
            + "  externalObjects: {}\n"
            + "  userData: \n"
            + "  assetBundleName: \n"
            + "  assetBundleVariant: \n";
        File.WriteAllText(
            packagePath.TrimEnd(Path.DirectorySeparatorChar) + ".meta",
            content,
            Utf8WithoutBom
        );
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    private static string ReadSecret()
    {
        string value = Environment.GetEnvironmentVariable(KeyName);
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();

        string envPath = ProjectPath(".env");
        if (!File.Exists(envPath))
            return null;
        foreach (string rawLine in File.ReadLines(envPath))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("#", StringComparison.Ordinal) || !line.Contains('='))
                continue;
            int equals = line.IndexOf('=');
            if (line.Substring(0, equals).Trim() == KeyName)
                return line.Substring(equals + 1).Trim().Trim('\'', '"');
        }
        return null;
    }

    private static string HashFile(string path)
    {
        using SHA256 sha = SHA256.Create();
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return string.Concat(sha.ComputeHash(input).Select(value => value.ToString("x2")));
    }
}
