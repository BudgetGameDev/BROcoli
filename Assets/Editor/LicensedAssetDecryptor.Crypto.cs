using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public sealed partial class LicensedAssetDecryptor
{
    private static string DecryptToTemporaryFile(string encryptedPath, string secret)
    {
        string temporaryRoot = ProjectPath("Library/BROcoli/LicensedAssets");
        Directory.CreateDirectory(temporaryRoot);
        string outputPath = Path.Combine(temporaryRoot, Guid.NewGuid().ToString("N") + ".payload");

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
