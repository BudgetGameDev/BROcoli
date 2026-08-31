using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class RuntimeTuningTests
    {
        private const BindingFlags Members =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        private GameObject host;
        private RuntimeTuning tuning;
        private string path;

        [SetUp]
        public void CreateTuning()
        {
            host = new GameObject("Runtime Tuning");
            tuning = host.AddComponent<RuntimeTuning>();
            path = Path.Combine(Path.GetTempPath(), "BrocoliTuning-" + Guid.NewGuid() + ".json");
        }

        [TearDown]
        public void DestroyTuning()
        {
            UnityEngine.Object.DestroyImmediate(host);
            if (File.Exists(path))
                File.Delete(path);
        }

        [Test]
        public void ApplyTargetsTheBrightestLightAndEveryConfiguredValue()
        {
            Light dim = new GameObject("Dim").AddComponent<Light>();
            Light key = new GameObject("Key").AddComponent<Light>();
            dim.intensity = 1f;
            key.intensity = 3f;
            try
            {
                LogAssert.Expect(
                    LogType.Log,
                    new System.Text.RegularExpressions.Regex("^\\[RuntimeTuning\\] applied")
                );
                Invoke(
                    "Apply",
                    new RuntimeTuning.TuningData
                    {
                        worldLightIntensity = 7f,
                        lightHeightY = 5f,
                        lightOffsetZ = -2f,
                        ambientIntensity = 1.5f,
                    }
                );

                Assert.That(key.intensity, Is.EqualTo(7f));
                Assert.That(key.transform.localPosition.y, Is.EqualTo(5f));
                Assert.That(key.transform.localPosition.z, Is.EqualTo(-2f));
                Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(1.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dim.gameObject);
                UnityEngine.Object.DestroyImmediate(key.gameObject);
            }
        }

        [Test]
        public void UpdateIgnoresMissingAndUnchangedFilesThenAppliesNewJson()
        {
            Set("_path", path);
            Set("_pollAcc", 1f);
            Invoke("Update");

            File.WriteAllText(path, "{\"ambientIntensity\":0.75}");
            Set("_pollAcc", 1f);
            LogAssert.Expect(
                LogType.Log,
                new System.Text.RegularExpressions.Regex("^\\[RuntimeTuning\\] applied")
            );
            Invoke("Update");
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(0.75f));

            Set("_pollAcc", 1f);
            Invoke("Update");
            Set("_pollAcc", 0f);
            Invoke("Update");
        }

        [Test]
        public void InvalidJsonIsReportedAndEnvironmentPathIsResolved()
        {
            File.WriteAllText(path, "{");
            Set("_path", path);
            Set("_pollAcc", 1f);
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("^\\[RuntimeTuning\\] parse failed:")
            );
            Invoke("Update");

            string previous = Environment.GetEnvironmentVariable("BROCOLI_TUNING");
            try
            {
                Environment.SetEnvironmentVariable("BROCOLI_TUNING", path);
                Assert.That(InvokeStatic("ResolvePath"), Is.EqualTo(path));
            }
            finally
            {
                Environment.SetEnvironmentVariable("BROCOLI_TUNING", previous);
            }
        }

        [Test]
        public void ExplicitCommandPathBootstrapsAWatcherWithoutEditorPersistence()
        {
            Assert.That(
                RuntimeTuning.ResolvePath(new[] { "game", "--tuning=/tmp/live.json" }, "env"),
                Is.EqualTo("/tmp/live.json")
            );
            LogAssert.Expect(LogType.Log, "[RuntimeTuning] watching /tmp/live.json");
            RuntimeTuning.Bootstrap("/tmp/live.json", _ => { });
            RuntimeTuning created = UnityEngine.Object.FindAnyObjectByType<RuntimeTuning>();
            Assert.That(created, Is.Not.Null);
            if (created.gameObject != host)
                UnityEngine.Object.DestroyImmediate(created.gameObject);
        }

        private object Invoke(string method, params object[] arguments) =>
            tuning.GetType().GetMethod(method, Members).Invoke(tuning, arguments);

        private static object InvokeStatic(string method, params object[] arguments) =>
            Array
                .Find(
                    typeof(RuntimeTuning).GetMethods(Members),
                    candidate =>
                        candidate.Name == method
                        && candidate.GetParameters().Length == arguments.Length
                )
                .Invoke(null, arguments);

        private void Set(string field, object value) =>
            tuning.GetType().GetField(field, Members).SetValue(tuning, value);
    }
}
