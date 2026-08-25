using UnityEngine;

/// <summary>
/// Locates the active screen-space UI canvas without accidentally selecting
/// the world-space health-bar canvases carried by enemies and projectiles.
/// </summary>
public static class ScreenCanvasLocator
{
    public static Canvas Find()
    {
        Canvas best = null;
        int bestScore = int.MinValue;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null
                || !canvas.gameObject.scene.isLoaded
                || !canvas.gameObject.activeInHierarchy
                || canvas.renderMode == RenderMode.WorldSpace)
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
