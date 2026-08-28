using UnityEngine;

public partial class PlayerStats
{
    /// <summary>
    /// Resolve the stats component gameplay actually drives: the one the tagged player
    /// root owns. UI and other observers must not fall back to an arbitrary
    /// <c>FindAnyObjectByType</c> result, because a second PlayerStats anywhere in the
    /// scene makes that pick change between scene loads, and the stray instance keeps
    /// reporting untouched default values.
    /// </summary>
    public static PlayerStats Resolve()
    {
        Transform player = ActivePlayerTarget;
        if (player == null)
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
                player = tagged.transform;
        }

        PlayerStats stats =
            player != null ? player.GetComponentInChildren<PlayerStats>(true) : null;
        return stats != null ? stats : FindAnyObjectByType<PlayerStats>();
    }

    private void WarnOnDuplicateStats()
    {
        Transform root = transform.root;
        if (root == null)
            return;

        PlayerStats[] onPlayer = root.GetComponentsInChildren<PlayerStats>(true);
        if (onPlayer.Length > 1)
        {
            Debug.LogWarning(
                $"PlayerStats: '{root.name}' carries {onPlayer.Length} PlayerStats components. "
                    + $"Only the one on '{onPlayer[0].gameObject.name}' is driven by gameplay; "
                    + "remove the extras so stat readouts cannot bind to a stale copy."
            );
        }
    }

    /// <summary>
    /// Discover UI Bar components by GameObject name.
    /// </summary>
    private void DiscoverUIComponents()
    {
        // Only inspect the screen HUD. Enemy prefabs also contain Bar components
        // on world-space canvases, and binding one of those makes player health or
        // XP appear to stop updating depending on spawn order.
        Canvas screenCanvas = ScreenCanvasLocator.Find();
        Bar[] allBars =
            screenCanvas != null
                ? screenCanvas.GetComponentsInChildren<Bar>(true)
                : FindObjectsByType<Bar>(FindObjectsSortMode.None);

        foreach (var bar in allBars)
        {
            if (bar.gameObject.name == "HealthBar")
            {
                _healthBar = bar;
            }
            else if (bar.gameObject.name == "ExperienceBar")
            {
                _experienceBar = bar;
            }
        }

        if (_healthBar == null)
        {
            Debug.LogWarning("PlayerStats: Could not find HealthBar in scene");
        }
        if (_experienceBar == null)
        {
            Debug.LogWarning("PlayerStats: Could not find ExperienceBar in scene");
        }
    }
}
