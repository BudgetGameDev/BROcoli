using System;
using BudgetGameDev.Autoplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Enters the game the way a player does: through the main menu, by pressing the
    /// button the menu itself wires up. Jumping straight to the dungeon scene is
    /// faster, but it leaves the menu, its save-slot handling, and the scene
    /// transition into a run completely untested.
    ///
    /// Starting a run claims a free save slot and repoints the active-slot
    /// preference. A bot run is a throwaway and must not leave that behind, so the
    /// two preferences it moves are captured here and put back when the run ends.
    /// </summary>
    public class AutoplaySessionDirector : MonoBehaviour
    {
        internal const string MenuScene = "Brocoli_MainMenu_Common";
        internal const string DungeonScene = "Brocoli_Dungeon_Common";

        private const string ControlPreferenceKey = "ShowVirtualController";

        /// <summary>Game-seconds to wait for the menu, and then for it to start a run.</summary>
        private const float StepTimeout = 20f;

        /// <summary>
        /// The director's only two effects on the world, named so a test can watch
        /// them instead of loading scenes it then has to unpick.
        /// </summary>
        internal Action<MainMenu> PressPlay = menu => menu.playGame();
        internal Action<string> LoadScene = SceneManager.LoadScene;

        private int restoreActiveSlot;
        private int restoreControls;
        private float elapsed;
        private float deadline;
        private bool pressedPlay;
        private bool captured;

        private void Start()
        {
            restoreActiveSlot = PlayerPrefs.GetInt(BrocoliSaveSystem.ActiveSlotKey, -1);
            restoreControls = PlayerPrefs.GetInt(ControlPreferenceKey, 0);
            captured = true;
            deadline = StepTimeout;
        }

        private void OnDestroy() => RestorePreferences();

        private void OnApplicationQuit() => RestorePreferences();

        internal void RestorePreferences()
        {
            if (!captured)
                return;

            captured = false;
            PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, restoreActiveSlot);
            PlayerPrefs.SetInt(ControlPreferenceKey, restoreControls);
            PlayerPrefs.Save();
        }

        private void Update()
        {
            // Only an autoplay run has any business loading scenes on its own.
            if (!AutoplayController.IsActive)
                return;

            if (SceneManager.GetActiveScene().name == DungeonScene)
            {
                enabled = false;
                return;
            }

            elapsed += AutoplayTimeControl.GameDelta;
            if (!pressedPlay)
                EnterRunFromMenu();
            else if (elapsed > deadline)
                GiveUpOnMenu("the main menu accepted Play but never loaded the dungeon");
        }

        /// <summary>
        /// Deliberately not named as a query: this presses Play, and a sweep that
        /// calls every <c>Try*</c> method looking for a safe read must not hit it.
        /// </summary>
        private void EnterRunFromMenu()
        {
            MainMenu menu = FindAnyObjectByType<MainMenu>();
            if (menu == null)
            {
                if (elapsed > deadline)
                    GiveUpOnMenu("the main menu never appeared");
                return;
            }

            AutoplayFeatureLog.Record(AutoplayFeatures.MainMenuShown);
            pressedPlay = true;
            deadline = elapsed + StepTimeout;
            PressPlay(menu);
        }

        /// <summary>
        /// Reports the menu as broken and carries on into the dungeon. The error
        /// already fails the run, and finishing it is what produces the report that
        /// says which of the remaining systems still work.
        /// </summary>
        private void GiveUpOnMenu(string reason)
        {
            Debug.LogError($"[Autoplay] Entering the dungeon directly because {reason}.");
            enabled = false;
            LoadScene(DungeonScene);
        }
    }
}
