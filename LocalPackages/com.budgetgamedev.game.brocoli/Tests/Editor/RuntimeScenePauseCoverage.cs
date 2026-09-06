using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static IEnumerator ReturnThroughPauseAndGameOverMenus()
        {
            PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>();
            Assert.That(pause, Is.Not.Null);
            pause.GoToMainMenu();
            yield return null;
            GameOverOverlay.Show(1, 1, 1, 1f).GoToMainMenu();
            yield return null;
        }

        private static void ResetEventSystemForLevelUp(LevelUpScreen levelUp)
        {
            foreach (
                EventSystem existing in Object.FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include
                )
            )
                Object.DestroyImmediate(existing.gameObject);
            InvokeHierarchy(levelUp, "EnsureEventSystemActive");
            EventSystem created = Object.FindAnyObjectByType<EventSystem>();
            Assert.That(created, Is.Not.Null);
            created.gameObject.SetActive(false);
            InvokeHierarchy(levelUp, "EnsureEventSystemActive");
        }

        private static EventSystem ExercisePauseEventSystemEdges(PauseMenu pause)
        {
            foreach (
                EventSystem events in Object.FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include
                )
            )
                events.gameObject.SetActive(false);
            SetHierarchyField(pause, "eventSystem", null);
            InvokeHierarchy(pause, "EnsureEventSystemActive");

            foreach (
                EventSystem events in Object.FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include
                )
            )
                Object.DestroyImmediate(events.gameObject);
            SetHierarchyField(pause, "eventSystem", null);
            LogAssert.Expect(LogType.Error, new Regex("^Failed setting EventSystem.current"));
            EventSystem.current = null;
            InvokeHierarchy(pause, "EnsureEventSystemActive");

            EventSystem replacement = GetHierarchyField<EventSystem>(pause, "eventSystem");
            LogAssert.Expect(LogType.Error, new Regex("^Failed setting EventSystem.current"));
            EventSystem.current = null;
            InvokeHierarchy(pause, "EnsureEventSystemActive");
            Assert.That(EventSystem.current, Is.SameAs(replacement));
            return replacement;
        }
    }
}
