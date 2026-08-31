using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class UIRemainderCoverageTests
    {
        [Test]
        public void MenuSelectionPauseAndMapCoverDestroyedAndNullEntries()
        {
            GameObject menuHost = new("Coverage menu selection");
            GameObject buttonHost = new(
                "Coverage menu button",
                typeof(RectTransform),
                typeof(Button)
            );
            GameObject pauseHost = new("Coverage pause selection");
            GameObject mapHost = new(
                "Coverage map remainder",
                typeof(RectTransform),
                typeof(DungeonMapGraphic)
            );
            GameObject dungeonHost = new("Coverage empty dungeon", typeof(DungeonManager));
            menuHost.SetActive(false);
            pauseHost.SetActive(false);
            dungeonHost.SetActive(false);
            try
            {
                MainMenu menu = menuHost.AddComponent<MainMenu>();
                Button button = buttonHost.GetComponent<Button>();
                Invoke(menu, "RegisterButtonVisual", button);
                Set(menu, "menuButtons", new[] { button });
                Set(menu, "selectedIndex", -1);
                Invoke(menu, "SelectButton", button);
                Object.DestroyImmediate(buttonHost);
                Invoke(menu, "ClearSelectionVisuals");
                Invoke(menu, "UpdateSelectionVisuals");

                PauseMenu pause = pauseHost.AddComponent<PauseMenu>();
                Set(pause, "menuButtons", new Button[] { null });
                Set(pause, "buttonOutlines", new Outline[1]);
                Set(pause, "originalScales", new Vector3[1]);
                Invoke(pause, "UpdateSelectionVisuals");
                Invoke(pause, "ResetMenuNavigation");

                DungeonMapGraphic map = mapHost.GetComponent<DungeonMapGraphic>();
                Set(map, "dungeon", dungeonHost.GetComponent<DungeonManager>());
                using var helper = new VertexHelper();
                Invoke(
                    map,
                    "DrawConnections",
                    helper,
                    new Rect(0f, 0f, 100f, 100f),
                    Vector2Int.zero,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.one
                );
            }
            finally
            {
                Object.DestroyImmediate(dungeonHost);
                Object.DestroyImmediate(mapHost);
                Object.DestroyImmediate(pauseHost);
                Object.DestroyImmediate(buttonHost);
                Object.DestroyImmediate(menuHost);
            }
        }
    }
}
