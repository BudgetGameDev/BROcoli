using UnityEngine;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Locates the active screen-space UI canvas without accidentally selecting
    /// the world-space health-bar canvases carried by enemies and projectiles.
    /// </summary>
    public static class ScreenCanvasLocator
    {
        /// <summary>
        /// Returns the screen canvas, creating one when the scene has none. A scene
        /// that only bootstraps its UI in code starts empty, so callers would
        /// otherwise have to duplicate this setup.
        /// </summary>
        public static Canvas GetOrCreate()
        {
            Canvas existing = Find();
            if (existing != null)
                return existing;

            var host = new GameObject(
                "Canvas",
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster)
            );
            Canvas canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = host.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject(
                    "EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)
                );

            return canvas;
        }

        public static Canvas Find()
        {
            Canvas best = null;
            int bestScore = int.MinValue;
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (Canvas canvas in canvases)
            {
                if (
                    canvas == null
                    || !canvas.gameObject.scene.isLoaded
                    || !canvas.gameObject.activeInHierarchy
                    || canvas.renderMode == RenderMode.WorldSpace
                )
                {
                    continue;
                }

                int score = 0;
                if (canvas.transform.parent == null)
                    score += 1000;
                if (canvas.gameObject.name == "Canvas")
                    score += 100;
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    score += 50;
                if (canvas.isRootCanvas)
                    score += 25;

                if (score > bestScore)
                {
                    best = canvas;
                    bestScore = score;
                }
            }

            return best;
        }
    }
}
