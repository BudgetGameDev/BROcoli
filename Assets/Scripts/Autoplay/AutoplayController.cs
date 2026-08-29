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
/// Deterministic mode advances a fixed amount of GAME time per rendered frame
/// (<see cref="Time.captureDeltaTime"/>) with rendering uncapped — i.e. "fake time"
/// fast-forward. Physics still runs at the fixed <c>Time.fixedDeltaTime</c> step
/// (sub-stepped per frame), so simulation stays accurate while wall-clock time is
/// compressed. A bigger <c>--timestep</c> compresses harder (coarser Update step).
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

        // Make each frame cheap so the fake-time fast-forward actually accelerates even
        // in heavy combat. Capturing stack traces for log/warning spam (thousands of
        // pool-capacity warnings) and high-res/quality rendering otherwise pin us near
        // real-time. Exceptions/errors keep their traces for debugging.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        QualitySettings.SetQualityLevel(0, true);

        // Small landscape window (keeps ForceLandscapeAspect happy; fewer pixels = faster).
        Screen.SetResolution(640, 360, false);

        if (_config.Deterministic)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            // Game-seconds advanced per rendered frame. Physics keeps its own fixed
            // step, so larger values compress wall-clock time without breaking the sim.
            Time.captureDeltaTime = Mathf.Clamp(_config.Timestep, 1f / 240f, 0.1f);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Skip the main menu and go straight to gameplay.
        SceneManager.LoadScene("Dungeon");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Dungeon" || _wired)
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

        Debug.Log(
            "[Autoplay] Dungeon scene wired (bot + capture + telemetry + level-up resolver)."
        );
    }
}

/// <summary>
/// Autoplay configuration parsed from command-line args and/or environment vars.
/// </summary>
public sealed class AutoplayConfig
{
    public bool Enabled;
    public int Seed = 12345;
    public float Duration = 60f; // game-seconds to simulate
    public float Interval = 0.5f; // game-seconds between samples/captures
    public string OutDir;
    public bool Deterministic = true;
    public float Timestep = 1f / 60f; // captureDeltaTime: game-seconds advanced per rendered frame
    public string Scenario = "survive"; // smoke | survive | progress
    public int MinLevel = 2; // pass threshold for the "progress" scenario
    public string Sha = ""; // git SHA, for the run manifest

    public static AutoplayConfig FromCommandLine()
    {
        var cfg = new AutoplayConfig();

        bool enabled = EnvFlag("BROCOLI_AUTOPLAY");
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            if (arg == "--autoplay")
                enabled = true;
            else if (arg == "--deterministic")
                cfg.Deterministic = true;
            else if (arg == "--no-deterministic")
                cfg.Deterministic = false;
            else if (arg.StartsWith("--seed="))
                TryInt(arg.Substring(7), ref cfg.Seed);
            else if (arg.StartsWith("--duration="))
                TryFloat(arg.Substring(11), ref cfg.Duration);
            else if (arg.StartsWith("--interval="))
                TryFloat(arg.Substring(11), ref cfg.Interval);
            else if (arg.StartsWith("--timestep="))
                TryFloat(arg.Substring(11), ref cfg.Timestep);
            else if (arg.StartsWith("--minlevel="))
                TryInt(arg.Substring(11), ref cfg.MinLevel);
            else if (arg.StartsWith("--out="))
                cfg.OutDir = arg.Substring(6);
            else if (arg.StartsWith("--scenario="))
                cfg.Scenario = arg.Substring(11);
            else if (arg.StartsWith("--sha="))
                cfg.Sha = arg.Substring(6);
        }
        cfg.Enabled = enabled;

        // Environment variables act as fallbacks/overrides (convenient from a shell).
        var s = Environment.GetEnvironmentVariable("BROCOLI_SEED");
        if (!string.IsNullOrEmpty(s))
            TryInt(s, ref cfg.Seed);
        var d = Environment.GetEnvironmentVariable("BROCOLI_DURATION");
        if (!string.IsNullOrEmpty(d))
            TryFloat(d, ref cfg.Duration);
        var i = Environment.GetEnvironmentVariable("BROCOLI_INTERVAL");
        if (!string.IsNullOrEmpty(i))
            TryFloat(i, ref cfg.Interval);
        var ts = Environment.GetEnvironmentVariable("BROCOLI_TIMESTEP");
        if (!string.IsNullOrEmpty(ts))
            TryFloat(ts, ref cfg.Timestep);
        var o = Environment.GetEnvironmentVariable("BROCOLI_OUT");
        if (!string.IsNullOrEmpty(o))
            cfg.OutDir = o;
        var sc = Environment.GetEnvironmentVariable("BROCOLI_SCENARIO");
        if (!string.IsNullOrEmpty(sc))
            cfg.Scenario = sc;

        if (string.IsNullOrEmpty(cfg.OutDir))
        {
            cfg.OutDir = Path.Combine(
                Directory.GetCurrentDirectory(),
                "AutoplayRuns",
                DateTime.Now.ToString("yyyyMMdd-HHmmss")
            );
        }

        return cfg;
    }

    public override string ToString() =>
        $"seed={Seed} duration={Duration}s interval={Interval}s timestep={Timestep:0.####} "
        + $"deterministic={Deterministic} scenario={Scenario} sha={Sha} out={OutDir}";

    private static bool EnvFlag(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryInt(string raw, ref int target)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            target = v;
    }

    private static void TryFloat(string raw, ref float target)
    {
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            target = v;
    }
}
