using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Autoplay helper: writes a PNG of the game's backbuffer every <c>interval</c>
/// game-seconds into <c>outDir/frames</c>. Sampling is frame-driven (accumulated
/// <see cref="Time.unscaledDeltaTime"/>), so in deterministic mode the captured
/// cadence is reproducible.
///
/// Uses <see cref="ScreenCapture.CaptureScreenshot(string)"/>, which grabs the
/// application's own framebuffer — no macOS Screen Recording permission required.
/// </summary>
public class FrameCapture : MonoBehaviour
{
    private string _framesDir;
    private float _interval = 0.5f;
    private int _index;

    public void Configure(AutoplayConfig cfg)
    {
        _framesDir = Path.Combine(cfg.OutDir, "frames");
        _interval = Mathf.Max(0.02f, cfg.Interval);
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(_framesDir))
        {
            Debug.LogWarning("[Autoplay] FrameCapture not configured; disabling.");
            enabled = false;
            return;
        }

        Directory.CreateDirectory(_framesDir);
        StartCoroutine(CaptureLoop());
    }

    private IEnumerator CaptureLoop()
    {
        float acc = 0f;
        bool first = true;
        while (true)
        {
            yield return new WaitForEndOfFrame();
            acc += Time.unscaledDeltaTime;
            if (first || acc >= _interval)
            {
                first = false;
                acc = 0f;
                ScreenCapture.CaptureScreenshot(Path.Combine(_framesDir, $"frame_{_index:D5}.png"));
                _index++;
            }
        }
    }
}
