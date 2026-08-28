using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Preloads and warms up assets, shaders, and prefabs before gameplay starts.
/// Shows a loading screen with progress bar while hiding game graphics.
/// This eliminates the hitch/stutter during the first wave.
/// </summary>
[DefaultExecutionOrder(-500)] // Run early, but after iOSSafariWebGLOptimizer
public partial class GamePreloader : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField]
    private bool warmupShaders = true;

    [SerializeField]
    private bool prewarmPrefabs = true;

    [SerializeField]
    private bool prewarmMaterials = true;

    [SerializeField]
    private bool prewarmPhysics = true;

    [SerializeField]
    private bool prewarmAudio = true;

    [SerializeField]
    private bool prewarmPools = true;

    [SerializeField]
    private int framesPerStep = 1;

    [Header("Loading Screen")]
    [SerializeField]
    private Color backgroundColor = Color.black;

    [SerializeField]
    private Color barBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    [SerializeField]
    private Color barFillColor = new Color(0.4f, 0.8f, 0.4f, 1f);

    [Header("Debug")]
    [SerializeField]
    private bool logPreloadSteps = false;

    private const string EnemyPrefabPath = "CursedDevolpmentStudioAss Assets/Waves";
    private const string BoostPrefabPath = "CursedDevolpmentStudioAss Assets";

    private static bool _hasPreloaded = false;
    private List<GameObject> _warmupInstances = new List<GameObject>();
    private LoadingScreenUI _loadingScreen;

    // Cached prefabs for pooling
    private List<GameObject> _enemyPrefabs = new List<GameObject>();
    private List<GameObject> _projectilePrefabs = new List<GameObject>();
    private ExpGain _expGainPrefab;

    private void Awake()
    {
        if (_hasPreloaded)
        {
            // Shaders, materials and audio stay warm for the whole process, but the
            // pools live on a scene-scoped PoolManager. A reloaded scene (restart after
            // death) gets a fresh, empty one, so they must be rebuilt here - otherwise
            // the ExpGain pool stays null, enemies drop no XP, and the player can never
            // level up or pick a single upgrade for the rest of the session.
            if (prewarmPools)
            {
                CollectPoolPrefabs();
                WarmupPools();
            }

            if (logPreloadSteps)
                Debug.Log("[GamePreloader] Already preloaded, rebuilt scene-scoped pools");
            Destroy(gameObject);
            return;
        }

        // Pause game time during loading so waves don't start
        Time.timeScale = 0f;

        _loadingScreen = new LoadingScreenUI(
            transform,
            backgroundColor,
            barBackgroundColor,
            barFillColor,
            "Loading..."
        );
        StartCoroutine(PreloadRoutine());
    }

    private IEnumerator PreloadRoutine()
    {
        float startTime = Time.realtimeSinceStartup;
        if (logPreloadSteps)
            Debug.Log("[GamePreloader] Starting preload...");

        // Step 1: Shader warmup (use WaitForSecondsRealtime since timeScale=0)
        if (warmupShaders)
        {
            _loadingScreen.SetText("Warming up shaders...");
            yield return null;
            WarmupShaders();
            _loadingScreen.SetProgress(0.06f);
            yield return WaitFramesRealtime(framesPerStep);
        }

        // Step 2: Prewarm materials
        if (prewarmMaterials)
        {
            _loadingScreen.SetText("Loading materials...");
            yield return null;
            SprayMaterialCreator.PrewarmAll();
            _loadingScreen.SetProgress(0.12f);
            yield return WaitFramesRealtime(framesPerStep);
        }

        // Step 3: Physics warmup
        if (prewarmPhysics)
        {
            _loadingScreen.SetText("Initializing physics...");
            yield return null;
            WarmupPhysics();
            _loadingScreen.SetProgress(0.18f);
            yield return WaitFramesRealtime(framesPerStep);
        }

        // Step 4: Audio warmup - pre-generate all procedural sound clips
        if (prewarmAudio)
        {
            _loadingScreen.SetText("Preparing audio...");
            yield return null;
            WarmupAudio();
            _loadingScreen.SetProgress(0.25f);
            yield return WaitFramesRealtime(framesPerStep);

            // Also warm up coroutine/audio systems to avoid first-use hitches
            yield return StartCoroutine(WarmupCoroutinesAndAudio());
            _loadingScreen.SetProgress(0.28f);
        }

        // Step 5: Prefab warmup (also collects prefabs for pooling)
        if (prewarmPrefabs)
        {
            _loadingScreen.SetText("Loading game assets...");
            yield return StartCoroutine(PrewarmPrefabs(0.28f, 0.70f));
        }

        // Step 6: Object pool warmup - pre-instantiate pooled objects
        if (prewarmPools)
        {
            _loadingScreen.SetText("Preparing object pools...");
            yield return null;
            WarmupPools();
            _loadingScreen.SetProgress(0.95f);
            yield return WaitFramesRealtime(framesPerStep);
        }

        _loadingScreen.SetProgress(1f);
        _loadingScreen.SetText("Ready!");
        yield return new WaitForSecondsRealtime(0.15f);

        _hasPreloaded = true;
        _loadingScreen.Destroy();

        // Resume game time
        Time.timeScale = 1f;

        if (logPreloadSteps)
            Debug.Log($"[GamePreloader] Complete in {Time.realtimeSinceStartup - startTime:F3}s");
    }

    private void WarmupShaders()
    {
        // NOTE: Shader.WarmupAllShaders() removed due to Unity bug causing
        // "State comes from an incompatible keyword space" errors when mixing
        // custom shaders with URP shaders. Using targeted shader loading instead.

        string[] shaders =
        {
            "Universal Render Pipeline/Particles/Lit",
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/2D/Sprite-Lit-Default",
            "Particles/Standard Unlit",
            "Sprites/Default",
        };
        foreach (string name in shaders)
            Shader.Find(name);
    }

    private void WarmupAudio()
    {
        // Pre-generate all boost pickup sounds (9 different types)
        ProceduralBoostAudio.PrewarmAll();

        // Pre-generate XP pickup sound
        ProceduralXPPickupAudio.PrewarmAll();

        // Pre-generate projectile hit sounds (player and enemy)
        ProceduralProjectileHitAudio.PrewarmAll();
        ProceduralEnemyProjectileHitAudio.PrewarmAll();

        // Pre-generate level up fanfare sound
        ProceduralLevelUpAudio.PrewarmAll();

        // Pre-generate player weapon sounds (5 gun types)
        ProceduralGunAudio.PrewarmAll();

        // Pre-generate player footstep sound
        ProceduralFootstepAudio.PrewarmAll();

        ProceduralEnemyMeleeAudio.PrewarmAll();
        ProceduralEnemyGunAudio.PrewarmAll();
        ProceduralEnemyWalkAudio.PrewarmAll(); // NEW: Pre-warm walk sounds
        EnemyDeathAudio.PrewarmAll();

        // UI audio pre-generates on initialization
        ProceduralUIAudio.PrewarmAll();

        if (logPreloadSteps)
            Debug.Log("[GamePreloader] Audio clips pre-generated");
    }

    private void WarmupPhysics()
    {
        LayerMask.GetMask("Enemy");
        LayerMask.GetMask("Player");
        LayerMask.GetMask("Default");
        GroundPlane.OverlapCircleAll(Vector2.zero, 0.1f);
        Physics.OverlapBox(Vector3.zero, Vector3.one * 0.1f);
    }

    /// <summary>
    /// Pre-warm Unity's coroutine and audio systems to avoid first-use hitches.
    /// </summary>
    private IEnumerator WarmupCoroutinesAndAudio()
    {
        // Pre-warm WaitForSeconds allocation (used by HitFlash, etc.)
        yield return new WaitForSecondsRealtime(0.001f);

        // Pre-warm AudioSource.PlayClipAtPoint (creates internal GameObject pool)
        // Use a silent clip to avoid any audible sound
        var silentClip = AudioClip.Create(
            "SilentWarmup",
            1,
            1,
            AudioSettings.outputSampleRate,
            false
        );
        silentClip.SetData(new float[] { 0f }, 0);
        AudioSource.PlayClipAtPoint(silentClip, new Vector3(-10000f, -10000f, 0f), 0f);

        yield return null;
    }

    private IEnumerator PrewarmPrefabs(float startProgress, float endProgress)
    {
        Vector3 warmupPos = new Vector3(-10000f, -10000f, 0f);
        List<GameObject> prefabsToWarm = CollectPoolPrefabs();

        // Instantiate each prefab offscreen
        for (int i = 0; i < prefabsToWarm.Count; i++)
        {
            float progress = Mathf.Lerp(startProgress, endProgress, (float)i / prefabsToWarm.Count);
            _loadingScreen.SetProgress(progress);

            GameObject instance = Instantiate(prefabsToWarm[i], warmupPos, Quaternion.identity);
            instance.name = $"[WARMUP] {prefabsToWarm[i].name}";
            DisableWarmupBehaviors(instance);
            _warmupInstances.Add(instance);

            yield return WaitFramesRealtime(framesPerStep);
        }

        // Cleanup
        yield return null;
        foreach (GameObject go in _warmupInstances)
            if (go != null)
                Destroy(go);
        _warmupInstances.Clear();
    }

    private void DisableWarmupBehaviors(GameObject go)
    {
        EnemyBase enemy = go.GetComponent<EnemyBase>();
        if (enemy != null)
            enemy.enabled = false;

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null)
            rb.SetSimulated(false);

        foreach (Collider col in go.GetComponents<Collider>())
            col.enabled = false;

        ExpGain exp = go.GetComponent<ExpGain>();
        if (exp != null)
            exp.enabled = false;
    }

    private IEnumerator WaitFramesRealtime(int count)
    {
        // Use WaitForEndOfFrame since timeScale=0 would freeze WaitForSeconds
        for (int i = 0; i < count; i++)
            yield return new WaitForEndOfFrame();
    }

    public static void ResetPreloadFlag() => _hasPreloaded = false;
}
