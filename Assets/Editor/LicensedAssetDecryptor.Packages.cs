using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

public sealed partial class LicensedAssetDecryptor
{
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
                throw new InvalidDataException(
                    "Licensed package exceeds extraction safety limits."
                );
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
            || parts.Any(part =>
                string.IsNullOrWhiteSpace(part)
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
        StringComparison comparison =
            Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        if (!destination.StartsWith(root + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException(
                $"Licensed package archive entry escapes its target: {relativePath}"
            );
        return destination;
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
}
