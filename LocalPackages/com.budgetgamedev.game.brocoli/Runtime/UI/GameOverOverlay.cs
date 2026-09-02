using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// In-scene game-over UI with run results and restart controls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class GameOverOverlay : MonoBehaviour
    {
        private static GameOverOverlay active;

        private Button restartButton;
        private Button mainMenuButton;
        private Button[] menuButtons;
        private Outline[] buttonOutlines;
        private Vector3[] originalButtonScales;
        private TextMeshProUGUI statsText;
        private int selectedIndex;

        public static GameOverOverlay Active => active;

        /// <summary>Whether the game-over screen is up anywhere.</summary>
        public static bool AnyVisible => active != null && active.IsVisible;

        public bool IsVisible { get; private set; }
        public int DisplayedScore { get; private set; }
        public int DisplayedRooms { get; private set; }
        public int DisplayedEnemiesKilled { get; private set; }
        public float DisplayedTimeSurvived { get; private set; }
        public Button RestartButton => restartButton;
        public Button MainMenuButton => mainMenuButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            active = null;
        }

        public static GameOverOverlay Show(int score, int rooms)
        {
            return Show(score, rooms, 0, 0f);
        }

        public static GameOverOverlay Show(
            int score,
            int rooms,
            int enemiesKilled,
            float timeSurvived
        )
        {
            if (active == null)
                active = CreateOverlay();

            active.Display(score, rooms, enemiesKilled, timeSurvived);
            return active;
        }

        private void Display(int score, int rooms, int enemiesKilled, float timeSurvived)
        {
            DisplayedScore = Mathf.Max(0, score);
            DisplayedRooms = Mathf.Max(0, rooms);
            DisplayedEnemiesKilled = Mathf.Max(0, enemiesKilled);
            DisplayedTimeSurvived = Mathf.Max(0f, timeSurvived);
            statsText.text =
                $"SCORE  {DisplayedScore:N0}\n"
                + $"ROOMS CLEARED  {DisplayedRooms:N0}\n"
                + $"ENEMIES KILLED  {DisplayedEnemiesKilled:N0}\n"
                + $"TIME SURVIVED  {GameStates.FormatSurvivalTime(DisplayedTimeSurvived)}";

            EnsureEventSystem();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsVisible = true;
            AutoplayFeatureLog.Record(AutoplayFeatures.GameOverShown);
            Time.timeScale = 0f;
            selectedIndex = 0;
            SelectButton(0);
            GameOverCTAManager.ShowForGameOverOverlay();
        }

        private static void EnsureEventSystem()
        {
            EventSystem system = EventSystem.current;
            if (system == null)
                system = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);

            if (system != null)
            {
                system.gameObject.SetActive(true);
                return;
            }

            GameObject eventSystemObject = new GameObject(
                "GameOverEventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            eventSystemObject.layer = LayerMask.NameToLayer("UI");
        }

        private void Update()
        {
            if (!IsVisible || menuButtons == null || menuButtons.Length == 0)
                return;

            GameObject selectedObject =
                EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (selectedObject == menuButtons[i].gameObject)
                {
                    selectedIndex = i;
                    break;
                }
            }

            for (int i = 0; i < menuButtons.Length; i++)
            {
                bool selected = i == selectedIndex;
                if (buttonOutlines[i] != null)
                    buttonOutlines[i].enabled = selected;
                Vector3 targetScale = originalButtonScales[i] * (selected ? 1.07f : 1f);
                menuButtons[i].transform.localScale = Vector3.Lerp(
                    menuButtons[i].transform.localScale,
                    targetScale,
                    Time.unscaledDeltaTime * 12f
                );
            }
        }

        private void SelectButton(int index)
        {
            if (index < 0 || index >= menuButtons.Length)
                return;

            selectedIndex = index;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(menuButtons[index].gameObject);
        }

        public void RestartGame()
        {
            AutoplayFeatureLog.Record(AutoplayFeatures.GameOverRestart);
            ProceduralUIAudio.PlaySelect();
            // Reload the active scene so a run restarts on a fresh dungeon.
            TransitionToScene(SceneManager.GetActiveScene().name);
        }

        public void GoToMainMenu()
        {
            ProceduralUIAudio.PlaySelect();
            TransitionToScene("Brocoli_MainMenu_Common");
        }

        private void TransitionToScene(string sceneName)
        {
            IsVisible = false;
            GameOverCTAManager.HideForGameOverOverlay();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            GameContext.ResetInstance();
            SceneManager.LoadScene(sceneName);
        }
    }
}
