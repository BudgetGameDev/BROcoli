using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseSharedRuntime()
        {
            if (Object.FindAnyObjectByType<GameAudioSettings>() == null)
                typeof(GameAudioSettings)
                    .GetMethod(
                        "Bootstrap",
                        System.Reflection.BindingFlags.Static
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .Invoke(null, null);
            GameObject duplicate = new("Coverage duplicate audio settings");
            GameAudioSettings settings = duplicate.AddComponent<GameAudioSettings>();
            InvokeHierarchy(settings, "Awake");
            ExerciseSingletons();
            ExerciseGraphicRegistryCleaner();
        }

        private static void ExerciseSingletons()
        {
            GameObject firstObject = new("Coverage Singleton First");
            CoverageSingleton first = firstObject.AddComponent<CoverageSingleton>();
            GameObject secondObject = new("Coverage Singleton Second");
            secondObject.AddComponent<CoverageSingleton>();
            LogAssert.Expect(
                LogType.Error,
                "There is more than one CoverageSingleton in the scene."
            );
            _ = CoverageSingleton.Instance;
            CoverageSingleton cached = CoverageSingleton.Instance;
            Assert.That(
                cached == first || cached == secondObject.GetComponent<CoverageSingleton>(),
                Is.True
            );
            _ = AutoCreatedSingleton.Instance;

            GameObject persistentObject = new("Coverage Persistent First");
            persistentObject.SetActive(false);
            CoveragePersistentSingleton persistent =
                persistentObject.AddComponent<CoveragePersistentSingleton>();
            persistent.Awake();
            Assert.That(CoveragePersistentSingleton.Instance, Is.SameAs(persistent));
            GameObject duplicateObject = new("Coverage Persistent Duplicate");
            duplicateObject.SetActive(false);
            CoveragePersistentSingleton persistentDuplicate =
                duplicateObject.AddComponent<CoveragePersistentSingleton>();
            persistentDuplicate.Awake();
        }
    }

    internal sealed class CoverageSingleton : Singleton<CoverageSingleton> { }

    internal sealed class AutoCreatedSingleton : Singleton<AutoCreatedSingleton> { }

    internal sealed class CoveragePersistentSingleton
        : SingletonPersistent<CoveragePersistentSingleton> { }
}
