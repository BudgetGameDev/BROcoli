using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lights the environment around the player without overexposing the player model.
///
/// On each gameplay scene it:
///   1. Moves the player's model renderers onto the "PlayerModel" layer
///      (particle effects parented to the player are left in the world).
///   2. Removes that layer from the brightest scene light's culling mask, so the
///      bright world light lights the ground/enemies but no longer hits the player.
///   3. Clones that light as a dimmer, player-only "fill" (same type/unit/range),
///      so the player stays readable instead of going flat/dark.
///
/// Self-bootstrapping (no scene wiring). No-op if the "PlayerModel" layer is absent.
/// </summary>
public class PlayerModelLighting : MonoBehaviour
{
    private const string LayerName = "PlayerModel";

    /// <summary>Default player fill brightness as a fraction of the world light's intensity.</summary>
    public const float DefaultFillFactor = 0.6f;

    /// <summary>The bright light that lights the world (player excluded). Null until applied.</summary>
    public static Light WorldLight { get; private set; }

    /// <summary>The dim, player-only fill light. Null until applied.</summary>
    public static Light FillLight { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[PlayerModelLighting]");
        DontDestroyOnLoad(go);
        go.AddComponent<PlayerModelLighting>();
    }

    private int _layer;
    private bool _applied;

    private void Awake()
    {
        _layer = LayerMask.NameToLayer(LayerName);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-apply for the freshly loaded scene's player.
        _applied = false;
    }

    private void Update()
    {
        if (_applied || _layer < 0)
            return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        var renderers = player.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return; // visual not spawned yet; try again next frame

        // 1) Player model renderers -> PlayerModel layer. Effects parented to the
        //    player are not part of its model: the ground fog rides along with the
        //    player but belongs to the world, and moving it here would cut it off
        //    from the world light and the torches.
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer)
                continue;
            r.gameObject.layer = _layer;
        }

        // 2) Brightest light = the world light. Exclude the player from it.
        Light main = null;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.gameObject.name == "PlayerFillLight")
                continue;
            if (main == null || l.intensity > main.intensity)
                main = l;
        }

        if (main != null)
        {
            main.cullingMask &= ~(1 << _layer);
            WorldLight = main;

            // 3) Clone it as a dimmer, player-only fill (keeps same type/unit/range).
            var fill = Instantiate(main, main.transform.parent);
            fill.name = "PlayerFillLight";
            fill.cullingMask = 1 << _layer;
            fill.intensity = main.intensity * DefaultFillFactor;
            fill.shadows = LightShadows.None;
            FillLight = fill;
        }

        _applied = true;
        Debug.Log(
            $"[PlayerModelLighting] Applied: {renderers.Length} renderer(s) -> layer {_layer}, "
                + $"world light excludes player, fill light added."
        );
    }
}
