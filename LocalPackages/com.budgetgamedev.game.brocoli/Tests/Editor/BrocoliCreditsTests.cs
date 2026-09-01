using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class BrocoliCreditsTests
    {
        [Test]
        public void CreditsResourceListsEveryDirectPackageDependency()
        {
            TextAsset credits = LoadCredits();
            PackageInfo package = PackageInfo.FindForAssembly(typeof(MainMenu).Assembly);

            Assert.That(package, Is.Not.Null);
            foreach (DependencyInfo dependency in package.dependencies)
            {
                Assert.That(
                    credits.text,
                    Does.Contain(dependency.name),
                    $"Credits omit package {dependency.name}."
                );
                Assert.That(
                    credits.text,
                    Does.Contain(dependency.version),
                    $"Credits omit {dependency.name} version {dependency.version}."
                );
            }
        }

        [Test]
        public void CreditsResourceListsEveryLockedProjectPackage()
        {
            TextAsset credits = LoadCredits();
            PackageInfo package = PackageInfo.FindForAssembly(typeof(MainMenu).Assembly);

            foreach ((string name, string version) in ReadLockedPackages(package))
            {
                Assert.That(credits.text, Does.Contain(name), $"Credits omit package {name}.");
                if (!version.StartsWith("file:"))
                {
                    Assert.That(
                        credits.text,
                        Does.Contain(version),
                        $"Credits omit {name} version {version}."
                    );
                }
            }
        }

        [Test]
        public void CreditsResourceListsEveryRestrictedAssetRecord()
        {
            TextAsset credits = LoadCredits();
            PackageInfo package = PackageInfo.FindForAssembly(typeof(MainMenu).Assembly);
            string metadataDirectory = Path.Combine(package.resolvedPath, "Encrypted/Licensed");
            string[] metadataFiles = Directory.GetFiles(metadataDirectory, "*.json");

            Assert.That(metadataFiles, Is.Not.Empty);
            foreach (string metadataFile in metadataFiles)
            {
                RestrictedAssetMetadata metadata = JsonUtility.FromJson<RestrictedAssetMetadata>(
                    File.ReadAllText(metadataFile)
                );
                AssertRecorded(credits.text, metadata.author, metadataFile, "author");
                AssertRecorded(credits.text, metadata.license, metadataFile, "license");
                AssertRecorded(credits.text, metadata.sourceUrl, metadataFile, "source URL");
                if (!string.IsNullOrWhiteSpace(metadata.title))
                    AssertRecorded(credits.text, metadata.title, metadataFile, "title");
            }
        }

        [TestCase("Dungeon wall torch")]
        [TestCase("UriZX")]
        [TestCase("Spray bottle")]
        [TestCase("naincube")]
        [TestCase("Mini Dungeon")]
        [TestCase("Modular Dungeon Kit")]
        [TestCase("splat.ogg")]
        [TestCase("gprosser")]
        [TestCase("Liberation Sans")]
        [TestCase("Texture Pack: Stylized 01")]
        [TestCase("Metal063")]
        [TestCase("GlazedTerracotta001")]
        [TestCase("Drachenfels Cellar")]
        [TestCase("Poly Haven")]
        [TestCase("Julio Sillet")]
        public void CreditsResourceContainsPublicAssetAttribution(string requiredText)
        {
            Assert.That(LoadCredits().text, Does.Contain(requiredText));
        }

        private static TextAsset LoadCredits()
        {
            TextAsset credits = Resources.Load<TextAsset>(
                ResponsiveMainMenuLayout.CreditsResourcePath
            );
            Assert.That(credits, Is.Not.Null, "The player-facing credits resource is missing.");
            return credits;
        }

        private static void AssertRecorded(
            string credits,
            string expected,
            string metadataFile,
            string field
        )
        {
            Assert.That(expected, Is.Not.Empty, $"{metadataFile} has no {field}.");
            Assert.That(
                credits,
                Does.Contain(expected),
                $"Credits omit {field} from {metadataFile}."
            );
        }

        private static IEnumerable<(string name, string version)> ReadLockedPackages(
            PackageInfo package
        )
        {
            string projectRoot = Path.GetFullPath(Path.Combine(package.resolvedPath, "../.."));
            string lockFile = Path.Combine(projectRoot, "Packages/packages-lock.json");
            string currentPackage = null;
            foreach (string line in File.ReadLines(lockFile))
            {
                if (line.StartsWith("    \"com.") && line.EndsWith("\": {"))
                {
                    currentPackage = line.Substring(5, line.Length - 9);
                    continue;
                }

                const string VersionPrefix = "\"version\": \"";
                string trimmed = line.Trim();
                if (currentPackage == null || !trimmed.StartsWith(VersionPrefix))
                    continue;

                int versionEnd = trimmed.IndexOf('"', VersionPrefix.Length);
                yield return (
                    currentPackage,
                    trimmed.Substring(VersionPrefix.Length, versionEnd - VersionPrefix.Length)
                );
                currentPackage = null;
            }
        }

        [System.Serializable]
        private sealed class RestrictedAssetMetadata
        {
            public string title;
            public string sourceUrl;
            public string author;
            public string license;
        }
    }
}
