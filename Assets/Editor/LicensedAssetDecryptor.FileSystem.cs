using System;
using System.IO;
using System.Threading;

public sealed partial class LicensedAssetDecryptor
{
    private const int FileSystemOperationMaximumAttempts = 6;
    private const int FileSystemOperationInitialDelayMilliseconds = 50;

    private static void DeleteDirectoryIfPresent(string path)
    {
        RetryFileSystemOperation(
            () =>
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            },
            $"delete directory '{path}'"
        );
    }

    private static void DeleteFileIfPresent(string path)
    {
        RetryFileSystemOperation(() => File.Delete(path), $"delete file '{path}'");
    }

    private static void MoveFileWithRetry(string sourcePath, string destinationPath)
    {
        RetryFileSystemOperation(
            () => File.Move(sourcePath, destinationPath),
            $"move file '{sourcePath}' to '{destinationPath}'"
        );
    }

    private static void MoveDirectoryWithRetry(string sourcePath, string destinationPath)
    {
        RetryFileSystemOperation(
            () => Directory.Move(sourcePath, destinationPath),
            $"move directory '{sourcePath}' to '{destinationPath}'"
        );
    }

    private static void RetryFileSystemOperation(Action operation, string description)
    {
        RetryFileSystemOperation(
            operation,
            description,
            Thread.Sleep,
            FileSystemOperationMaximumAttempts
        );
    }

    private static void RetryFileSystemOperation(
        Action operation,
        string description,
        Action<int> delay,
        int maximumAttempts
    )
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        if (delay == null)
            throw new ArgumentNullException(nameof(delay));
        if (maximumAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        int delayMilliseconds = FileSystemOperationInitialDelayMilliseconds;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                operation();
                return;
            }
            catch (Exception exception) when (IsRetryableFileSystemException(exception))
            {
                if (attempt >= maximumAttempts)
                {
                    exception.Data["LicensedAssetOperation"] = description;
                    exception.Data["LicensedAssetAttempts"] = attempt;
                    throw;
                }

                delay(delayMilliseconds);
                delayMilliseconds *= 2;
            }
        }
    }

    private static bool IsRetryableFileSystemException(Exception exception)
    {
        if (exception is UnauthorizedAccessException)
            return true;
        return exception is IOException
            && exception is not FileNotFoundException
            && exception is not DirectoryNotFoundException
            && exception is not DriveNotFoundException
            && exception is not PathTooLongException;
    }
}
