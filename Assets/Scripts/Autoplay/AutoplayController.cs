using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Entry point for automated playtest ("autoplay") runs. Activated only when the
/// build/editor is launched with <c>--autoplay</c> (or env <c>BROCOLI_AUTOPLAY=1</c>).
/// When not requested this class does nothing, so normal play is unaffected.
///
/// On activation it seeds RNG, optionally enables a deterministic fixed timestep,
/// forces a landscape window, jumps straight to the <c>Game</c> scene, and wires up
/// the bot driver, frame capture, telemetry, and the level-up auto-resolver.
/// See plans/2026-06-13-autoplay-e2e-harness.md.
/// </summary>
public class AutoplayController : MonoBehaviour
{
    public static bool IsActive { get; private set; }

    private AutoplayConfig _config;
    private bool _wired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var config = AutoplayConfig.FromCommandLine();
        if (!config.Enabled)
            return;

        IsActive = true;

        var go = new GameObject("[AutoplayController]");
        DontDestroyOnLoad(go);
        var controller = go.AddComponent<AutoplayController>();
        controller._config = config;
        controller.Begin();
    }

    private void Begin()
    {
        Debug.Log($"[Autoplay] Starting. {_config}");

        Application.runInBackground = true;
        UnityEngine.Random.InitState(_config.Seed);

        // Force a landscape window so ForceLandscapeAspect does not pause the run.
        Screen.SetResolution(1280, 720, false);

        // Deterministic mode: fixed simulation step decoupled from real frame rate,
        // so two runs with the same seed produce the same gameplay. The run then
        // executes as fast as it can render.
        if (_config.Deterministic)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = 1f / 60f;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Skip the main menu and go straight to gameplay.
        SceneManager.LoadScene("Game");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Game" || _wired)
            return;

        _wired = true;

        // Re-assert the seed right before gameplay objects spin up.
        UnityEngine.Random.InitState(_config.Seed);

        gameObject.AddComponent<BotDriver>();
        gameObject.AddComponent<LevelUpAutoResolver>();

        var capture = gameObject.AddComponent<FrameCapture>();
        capture.Configure(_config);

        var telemetry = gameObject.AddComponent<RunTelemetry>();
        telemetry.Configure(_config);

        Debug.Log("[Autoplay] Game scene wired (bot + capture + telemetry + level-up resolver).");
    }
}

/// <summary>
/// Autoplay configuration parsed from command-line args and/or environment vars.
/// </summary>
public sealed class AutoplayConfig
{
    public bool Enabled;
    public int Seed = 12345;
    public float Duration = 60f;        // game-seconds
    public float Interval = 0.5f;       // game-seconds between samples/captures
    public string OutDir;
    public bool Deterministic = true;
    public string Scenario = "survive"; // smoke | survive | progress
    public int MinLevel = 2;            // pass threshold for the "progress" scenario
    public string Sha = "";             // git SHA, for the run manifest

    public static AutoplayConfig FromCommandLine()
    {
        var cfg = new AutoplayConfig();

        bool enabled = EnvFlag("BROCOLI_AUTOPLAY");
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            if (arg == "--autoplay") enabled = true;
            else if (arg == "--deterministic") cfg.Deterministic = true;
            else if (arg == "--no-deterministic") cfg.Deterministic = false;
            else if (arg.StartsWith("--seed=")) TryInt(arg.Substring(7), ref cfg.Seed);
            else if (arg.StartsWith("--duration=")) TryFloat(arg.Substring(11), ref cfg.Duration);
            else if (arg.StartsWith("--interval=")) TryFloat(arg.Substring(11), ref cfg.Interval);
            else if (arg.StartsWith("--minlevel=")) TryInt(arg.Substring(11), ref cfg.MinLevel);
            else if (arg.StartsWith("--out=")) cfg.OutDir = arg.Substring(6);
            else if (arg.StartsWith("--scenario=")) cfg.Scenario = arg.Substring(11);
            else if (arg.StartsWith("--sha=")) cfg.Sha = arg.Substring(6);
        }
        cfg.Enabled = enabled;

        // Environment variables act as fallbacks/overrides (convenient from a shell).
        var s = Environment.GetEnvironmentVariable("BROCOLI_SEED"); if (!string.IsNullOrEmpty(s)) TryInt(s, ref cfg.Seed);
        var d = Environment.GetEnvironmentVariable("BROCOLI_DURATION"); if (!string.IsNullOrEmpty(d)) TryFloat(d, ref cfg.Duration);
        var i = Environment.GetEnvironmentVariable("BROCOLI_INTERVAL"); if (!string.IsNullOrEmpty(i)) TryFloat(i, ref cfg.Interval);
        var o = Environment.GetEnvironmentVariable("BROCOLI_OUT"); if (!string.IsNullOrEmpty(o)) cfg.OutDir = o;
        var sc = Environment.GetEnvironmentVariable("BROCOLI_SCENARIO"); if (!string.IsNullOrEmpty(sc)) cfg.Scenario = sc;

        if (string.IsNullOrEmpty(cfg.OutDir))
        {
            cfg.OutDir = Path.Combine(Directory.GetCurrentDirectory(), "AutoplayRuns",
                DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        return cfg;
    }

    public override string ToString() =>
        $"seed={Seed} duration={Duration}s interval={Interval}s deterministic={Deterministic} " +
        $"scenario={Scenario} sha={Sha} out={OutDir}";

    private static bool EnvFlag(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryInt(string raw, ref int target)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) target = v;
    }

    private static void TryFloat(string raw, ref float target)
    {
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) target = v;
    }
}
