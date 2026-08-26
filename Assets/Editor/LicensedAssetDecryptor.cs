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
public sealed class LicensedAssetDecryptor : IPreprocessBuildWithReport
{
    private const string KeyName = "BROCOLI_LICENSED_ASSET_KEY";
    private const string EncryptedRoot = "Assets/Encrypted/Licensed";
    private const string GeneratedRoot = "Assets/Resources/Generated/Licensed/";
    private const int Iterations = 200000;
    private static readonly byte[] OpenSslMagic = Encoding.ASCII.GetBytes("Salted__");

    [Serializable]
    private sealed class AssetMetadata
    {
        public int formatVersion;
        public string generatedPath;
        public string sha256;
        public string sourceUrl;
        public string author;
        public string license;
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
        if (!Directory.Exists(EncryptedRoot))
            return true;

        string[] encryptedFiles = Directory.GetFiles(EncryptedRoot, "*.enc");
        if (encryptedFiles.Length == 0)
            return true;

        string secret = ReadSecret();
        if (string.IsNullOrWhiteSpace(secret))
        {
            if (logMissingKey)
                Debug.LogError($"Missing {KeyName}; licensed models cannot be generated.");
            return false;
        }

        bool changed = false;
        foreach (string encryptedPath in encryptedFiles)
            changed |= DecryptAsset(encryptedPath, secret);

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
        if (
            File.Exists(metadata.generatedPath)
            && HashFile(metadata.generatedPath) == metadata.sha256
        )
            return false;

        byte[] plaintext = DecryptOpenSsl(File.ReadAllBytes(encryptedPath), secret);
        string actualHash = HashBytes(plaintext);
        if (!actualHash.Equals(metadata.sha256, StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException(
                $"Wrong key or damaged licensed asset: {encryptedPath}"
            );

        string directory = Path.GetDirectoryName(metadata.generatedPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(metadata.generatedPath, plaintext);
        Debug.Log($"Generated licensed asset for {metadata.author}: {metadata.generatedPath}");
        return true;
    }

    private static void ValidateMetadata(AssetMetadata metadata, string metadataPath)
    {
        if (
            metadata == null
            || metadata.formatVersion != 1
            || string.IsNullOrWhiteSpace(metadata.sha256)
        )
            throw new InvalidDataException($"Invalid licensed asset metadata: {metadataPath}");
        string normalized = metadata.generatedPath?.Replace('\\', '/');
        if (
            string.IsNullOrEmpty(normalized)
            || !normalized.StartsWith(GeneratedRoot, StringComparison.Ordinal)
        )
            throw new InvalidDataException($"Generated path must stay under {GeneratedRoot}");
    }

    private static byte[] DecryptOpenSsl(byte[] payload, string secret)
    {
        if (payload.Length < 32 || !payload.Take(8).SequenceEqual(OpenSslMagic))
            throw new InvalidDataException(
                "Encrypted asset does not use the expected OpenSSL format."
            );

        byte[] salt = payload.Skip(8).Take(8).ToArray();
        byte[] ciphertext = payload.Skip(16).ToArray();
        using var derivation = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(secret),
            salt,
            Iterations,
            HashAlgorithmName.SHA256
        );
        byte[] key = derivation.GetBytes(32);
        byte[] iv = derivation.GetBytes(16);
        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
    }

    private static string ReadSecret()
    {
        string value = Environment.GetEnvironmentVariable(KeyName);
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();

        string envPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".env"));
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

    private static string HashFile(string path) => HashBytes(File.ReadAllBytes(path));

    private static string HashBytes(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
    }
}
