using System;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class LicensedAssetDecryptor
{
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
            || parts.Any(part =>
                string.IsNullOrWhiteSpace(part)
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
        StringComparison comparison =
            Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        if (!result.StartsWith(projectRoot + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException($"Project path escapes the project: {relativePath}");
        return result;
    }
}
