using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Live ("hot-reload") tuning of lighting while the game runs, so values can be
/// iterated WITHOUT rebuilding. Active only when launched with <c>--tuning=PATH</c>
/// (or env <c>BROCOLI_TUNING=PATH</c>). Polls the JSON file and re-applies whenever
/// it changes.
///
/// JSON keys (omit any you don't want to change):
///   { "worldLightIntensity": 250, "fillFactor": 0.6, "lightHeightY": 8, "ambientIntensity": 1 }
///
/// Pair with scripts/autoplay-tune.sh (a long, real-time session) to tune lighting
/// by editing the file while watching captured frames.
/// </summary>
public class RuntimeTuning : MonoBehaviour
{
    private const float Unset = -999f;

    [Serializable]
    public class TuningData
    {
        public float worldLightIntensity = Unset;
        public float fillFactor = Unset;
        public float lightHeightY = Unset;
        public float ambientIntensity = Unset;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string path = ResolvePath();
        if (string.IsNullOrEmpty(path))
            return;

        var go = new GameObject("[RuntimeTuning]");
        DontDestroyOnLoad(go);
        go.AddComponent<RuntimeTuning>()._path = path;
        Debug.Log($"[RuntimeTuning] watching {path}");
    }

    private static string ResolvePath()
    {
        foreach (var a in Environment.GetCommandLineArgs())
            if (a.StartsWith("--tuning="))
                return a.Substring(9);
        return Environment.GetEnvironmentVariable("BROCOLI_TUNING");
    }

    private string _path;
    private float _fillFactor = PlayerModelLighting.DefaultFillFactor;
    private long _lastTicks;
    private float _pollAcc;

    private void Update()
    {
        _pollAcc += Time.unscaledDeltaTime;
        if (_pollAcc < 0.5f)
            return;
        _pollAcc = 0f;

        if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
            return;

        long ticks = File.GetLastWriteTimeUtc(_path).Ticks;
        if (ticks == _lastTicks)
            return;
        _lastTicks = ticks;

        var data = new TuningData();
        try
        {
            JsonUtility.FromJsonOverwrite(File.ReadAllText(_path), data);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RuntimeTuning] parse failed: {e.Message}");
            return;
        }

        Apply(data);
    }

    private void Apply(TuningData d)
    {
        var world = PlayerModelLighting.WorldLight;
        var fill = PlayerModelLighting.FillLight;

        if (Set(d.fillFactor))
            _fillFactor = d.fillFactor;

        if (world != null && Set(d.worldLightIntensity))
            world.intensity = d.worldLightIntensity;

        if (Set(d.lightHeightY))
        {
            if (world != null)
                SetLocalY(world.transform, d.lightHeightY);
            if (fill != null)
                SetLocalY(fill.transform, d.lightHeightY);
        }

        if (world != null && fill != null)
            fill.intensity = world.intensity * _fillFactor;

        if (Set(d.ambientIntensity))
            RenderSettings.ambientIntensity = d.ambientIntensity;

        Debug.Log(
            $"[RuntimeTuning] applied worldI={(world != null ? world.intensity : 0f)} "
                + $"fillFactor={_fillFactor} y={(world != null ? world.transform.localPosition.y : 0f)} "
                + $"ambient={RenderSettings.ambientIntensity}"
        );
    }

    private static bool Set(float v) => v > Unset + 1f;

    private static void SetLocalY(Transform t, float y)
    {
        var p = t.localPosition;
        p.y = y;
        t.localPosition = p;
    }
}
