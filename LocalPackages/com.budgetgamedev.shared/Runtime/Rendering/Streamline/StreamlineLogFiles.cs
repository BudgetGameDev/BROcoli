using System;
using System.IO;
using System.Linq;
using System.Text;

namespace BudgetGameDev.Shared.Rendering
{
    internal static class StreamlineLogFiles
    {
        internal const int FileLimit = 256 * 1024;
        private const int TotalLimit = 2 * 1024 * 1024;

        internal static string Read(string directory, string playerLog)
        {
            var text = new StringBuilder("\n\nACTUAL LOG FILES • captured on copy/export\n");
            int remaining = TotalLimit;
            text.AppendLine(
                "Current native session: "
                    + (string.IsNullOrEmpty(directory) ? "unavailable" : directory)
            );
            try
            {
                var files =
                    string.IsNullOrEmpty(directory) || !Directory.Exists(directory)
                        ? Array.Empty<string>()
                        : Directory
                            .GetFiles(directory, "*", SearchOption.AllDirectories)
                            .Where(path =>
                                string.Equals(
                                    Path.GetExtension(path),
                                    ".log",
                                    StringComparison.OrdinalIgnoreCase
                                )
                                || string.Equals(
                                    Path.GetExtension(path),
                                    ".txt",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            .OrderBy(path => path, StringComparer.Ordinal)
                            .ToArray();
                if (files.Length == 0)
                    text.AppendLine(
                        "No Streamline/NGX files exist for this session. The SDK may not have started."
                    );
                foreach (string path in files)
                    Append(text, path, ref remaining);
            }
            catch (Exception error)
                when (error is IOException || error is UnauthorizedAccessException)
            {
                text.AppendLine("Cannot enumerate native logs: " + error.Message);
            }
            if (!string.IsNullOrEmpty(playerLog))
                Append(text, playerLog, ref remaining);
            return text.ToString();
        }

        private static void Append(StringBuilder text, string path, ref int remaining)
        {
            text.AppendLine("\n--- FILE: " + path + " ---");
            if (remaining == 0)
            {
                text.AppendLine("[Omitted: 2 MiB total copy limit reached.]");
                return;
            }
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete
                );
                long size = stream.Length;
                int count = (int)Math.Min(size, Math.Min(FileLimit, remaining));
                text.AppendLine(
                    $"Size: {size} bytes; modified UTC: {File.GetLastWriteTimeUtc(path):O}"
                );
                if (size > count)
                    text.AppendLine(
                        $"[Truncated: last {count} of {size} bytes. Full file at path above.]"
                    );
                stream.Seek(size - count, SeekOrigin.Begin);
                var bytes = new byte[count];
                int read = 0,
                    next;
                while (read < count && (next = stream.Read(bytes, read, count - read)) > 0)
                    read += next;
                text.AppendLine(Encoding.UTF8.GetString(bytes, 0, read));
                remaining -= count;
            }
            catch (Exception error)
                when (error is IOException || error is UnauthorizedAccessException)
            {
                text.AppendLine("[Could not read file: " + error.Message + "]");
            }
            text.AppendLine("--- END FILE ---");
        }
    }
}
