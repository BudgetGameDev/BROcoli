using System;
using System.IO;
using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class StreamlineLogFilesTests
    {
        [Test]
        public void CopyIncludesActualSdkFilesWhileOpenAndMarksTruncatedTails()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "sl.log");
                using var writer = new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite
                );
                byte[] data = System.Text.Encoding.UTF8.GetBytes(
                    new string('x', StreamlineLogFiles.FileLimit) + "\nNGX actual error Ω"
                );
                writer.Write(data, 0, data.Length);
                writer.Flush();
                File.WriteAllText(
                    Path.Combine(directory, "bridge.log"),
                    "Signature failed before slInit"
                );
                File.WriteAllText(Path.Combine(directory, "ignored.json"), "Not a log");
                string report = StreamlineLogFiles.Read(directory, "");
                Assert.That(
                    report,
                    Does.Contain("NGX actual error Ω").And.Contain("Signature failed before slInit")
                );
                Assert.That(report, Does.Contain("Truncated:").And.Contain(path));
                Assert.That(report, Does.Not.Contain("Not a log"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void MissingFilesAreReportedWithoutHidingDiagnostics()
        {
            string report = StreamlineLogFiles.Read(
                "",
                Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".log")
            );
            Assert.That(
                report,
                Does.Contain("SDK may not have started").And.Contain("Could not read file")
            );
        }
    }
}
