using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        /// <summary>
        /// The presses the save journey makes in the menu, against the real save panel.
        /// The two buttons that would carry a run away are swapped for stand-ins first:
        /// what is under test is that the journey finds the panel's controls and the row
        /// holding the run it asked for, and a real press would take the scene with it
        /// and end the smoke run here.
        /// </summary>
        private static void ExerciseSaveJourneyMenuPresses(ResponsiveMainMenuLayout layout)
        {
            GameObject standInObject = new(
                "Coverage Journey Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            Button standIn = standInObject.GetComponent<Button>();
            int presses = 0;
            standIn.onClick.AddListener(() => presses++);

            AutoplaySaveJourneyDirector director = CreateIdleJourneyDirector();
            ExerciseJourneyResumePress(layout, director, standIn, () => presses);
            ExerciseJourneyNewRunPress(layout, director, standIn, () => presses);

            Object.DestroyImmediate(director.gameObject);
            Object.DestroyImmediate(standInObject);
        }

        /// <summary>
        /// Resuming: the panel opens, the row holding that run is picked, and Play is
        /// pressed. A slot no row is showing is a run the journey cannot resume, which
        /// is what a save list that has lost track of its runs looks like.
        /// </summary>
        private static void ExerciseJourneyResumePress(
            ResponsiveMainMenuLayout layout,
            AutoplaySaveJourneyDirector director,
            Button standIn,
            System.Func<int> presses
        )
        {
            const int listedSlot = 6;
            Button play = GetHierarchyField<Button>(layout, "playSaveButton");
            IList rows = (IList)GetHierarchyField<object>(layout, "saveRows");
            FieldInfo slotField = rows[0].GetType().GetField("Slot");

            InvokeHierarchy(layout, "CloseSaves");
            SetHierarchyField(layout, "playSaveButton", standIn);
            Assert.That(layout.PressPlayOnRun(listedSlot), Is.False, "no row shows that run");

            // The panel is open from here, so the rows can be pointed at a known run
            // without a refresh putting the real save list back.
            slotField.SetValue(rows[0], listedSlot);
            SetHierarchyField(layout, "visibleSaveCount", 1);
            SetHierarchyField(layout, "selectedRow", 0);
            standIn.interactable = true;
            int before = presses();
            Assert.That((bool)InvokeHierarchy(director, "PressPlayOnRun", listedSlot), Is.True);
            Assert.That(presses(), Is.EqualTo(before + 1));

            Assert.That(
                (bool)InvokeHierarchy(director, "PressPlayOnRun", -1),
                Is.False,
                "a run with no slot is nothing to press Play on"
            );
            SetHierarchyField(layout, "playSaveButton", play);
        }

        /// <summary>Starting another character: the same panel, its other button.</summary>
        private static void ExerciseJourneyNewRunPress(
            ResponsiveMainMenuLayout layout,
            AutoplaySaveJourneyDirector director,
            Button standIn,
            System.Func<int> presses
        )
        {
            Button newRun = GetHierarchyField<Button>(layout, "newRunButton");

            InvokeHierarchy(layout, "CloseSaves");
            SetHierarchyField(layout, "newRunButton", standIn);
            layout.PressNewRun(); // opens the panel on its way in, whatever it decides

            standIn.interactable = true;
            int before = presses();
            Assert.That((bool)InvokeHierarchy(director, "PressNewRun"), Is.True);
            Assert.That(presses(), Is.EqualTo(before + 1));

            standIn.interactable = false;
            Assert.That(layout.PressNewRun(), Is.False, "a full save list offers no new run");
            SetHierarchyField(layout, "newRunButton", null);
            Assert.That(layout.PressNewRun(), Is.False);
            SetHierarchyField(layout, "newRunButton", newRun);
        }

        /// <summary>
        /// Leaving a run and losing one: the two things the journey does inside the
        /// dungeon. The pause menu's own Main Menu button is swapped out for the same
        /// reason as the panel's, but the pause is real, and so is the killing blow --
        /// it is thrown through the entry point an enemy's strike lands on, which is
        /// also why it may have to be thrown more than once.
        /// </summary>
        private static IEnumerator ExerciseSaveJourneyRunPresses(PlayerStats stats)
        {
            AutoplaySaveJourneyDirector director = CreateIdleJourneyDirector();
            PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>();
            Assert.That(pause, Is.Not.Null);

            Button owned = pause.mainMenuButton;
            pause.mainMenuButton = null;
            Assert.That(
                (bool)InvokeHierarchy(director, "PressQuitToMenu"),
                Is.False,
                "there is no way out of the run to press"
            );

            GameObject standInObject = new(
                "Coverage Quit Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            int quits = 0;
            pause.mainMenuButton = standInObject.GetComponent<Button>();
            pause.mainMenuButton.onClick.AddListener(() => quits++);

            Assert.That((bool)InvokeHierarchy(director, "PressQuitToMenu"), Is.True);
            Assert.That(quits, Is.EqualTo(1));
            Assert.That(pause.IsPaused(), Is.True, "a player pauses before leaving");
            Assert.That((bool)InvokeHierarchy(director, "PressQuitToMenu"), Is.True);
            Assert.That(quits, Is.EqualTo(2), "pressing again from an open pause menu");

            pause.Resume();
            pause.mainMenuButton = owned;
            Object.DestroyImmediate(standInObject);
            yield return null;

            Assert.That(stats.IsAlive, Is.True, "there is still a run to lose");
            bool over = false;

            // The damage handler holds an immunity window open after every hit, so the
            // blow is thrown until one lands -- which is what the journey does too.
            for (int attempt = 0; attempt < 240 && !over; attempt++)
            {
                over = (bool)InvokeHierarchy(director, "TakeAFatalHit");
                yield return null;
            }

            Assert.That(over, Is.True);
            Assert.That(stats.IsAlive, Is.False);
            Assert.That(
                (bool)InvokeHierarchy(director, "TakeAFatalHit"),
                Is.True,
                "a run already over stays over rather than being hit again"
            );

            Object.DestroyImmediate(director.gameObject);
        }

        /// <summary>
        /// A director that will not drive anything. The journey steers scenes on its
        /// own, which is the last thing a smoke run wants, so this one is parked on an
        /// inactive object and only ever has its presses called by hand.
        /// </summary>
        private static AutoplaySaveJourneyDirector CreateIdleJourneyDirector()
        {
            GameObject host = new("Coverage Save Journey");
            host.SetActive(false);
            return host.AddComponent<AutoplaySaveJourneyDirector>();
        }
    }
}
