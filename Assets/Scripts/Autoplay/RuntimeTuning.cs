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
///   { "worldLightIntensity": 85, "lightHeightY": 5, "lightOffsetZ": -5, "ambientIntensity": 1 }
///
/// The light being tuned is the player's key light, which lights the player and the
/// world alike. <c>lightOffsetZ</c> is the one that shapes the characters: the camera
/// looks down +Z, so a negative offset swings the light toward it and off the crown of
/// the head, and 0 flattens everything into a straight-overhead wash.
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
        public float lightHeightY = Unset;
        public float lightOffsetZ = Unset;
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
    private Light _key;
    private long _lastTicks;
    private float _pollAcc;

    /// <summary>
    /// The key light, resolved as the brightest light in the scene. The torches sit an
    /// order of magnitude below it, so brightness picks it out without scene wiring.
    /// Re-resolved whenever it goes null, which is what a scene load looks like here.
    /// </summary>
    private Light KeyLight
    {
        get
        {
            if (_key != null)
                return _key;

            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (_key == null || l.intensity > _key.intensity)
                    _key = l;

            return _key;
        }
    }

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
        var key = KeyLight;

        if (key != null)
        {
            if (Set(d.worldLightIntensity))
                key.intensity = d.worldLightIntensity;

            var p = key.transform.localPosition;
            if (Set(d.lightHeightY))
                p.y = d.lightHeightY;
            if (Set(d.lightOffsetZ))
                p.z = d.lightOffsetZ;
            key.transform.localPosition = p;
        }

        if (Set(d.ambientIntensity))
            RenderSettings.ambientIntensity = d.ambientIntensity;

        Debug.Log(
            $"[RuntimeTuning] applied worldI={(key != null ? key.intensity : 0f)} "
                + $"pos={(key != null ? key.transform.localPosition : Vector3.zero)} "
                + $"ambient={RenderSettings.ambientIntensity}"
        );
    }

    private static bool Set(float v) => v > Unset + 1f;
}
