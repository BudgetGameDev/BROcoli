using System.Collections;
using System.IO;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Autoplay helper: writes a PNG of the game's backbuffer every <c>interval</c>
    /// game-seconds into <c>outDir/frames</c>, and one into <c>outDir/events</c> for
    /// each <c>--capture-on</c> trigger that fires. Sampling is frame-driven
    /// (accumulated <see cref="Time.unscaledDeltaTime"/>), so in deterministic mode
    /// the captured cadence is reproducible.
    ///
    /// Uses <see cref="ScreenCapture.CaptureScreenshot(string)"/>, which grabs the
    /// application's own framebuffer — no macOS Screen Recording permission required.
    /// Unity keeps only the last screenshot requested in a frame, so the loop takes
    /// at most one picture per frame and lets the rest wait.
    /// </summary>
    public class FrameCapture : MonoBehaviour
    {
        private string _framesDir;
        private string _eventsDir;
        private string _eventsPath;
        private float _interval = 0.5f;
        private int _index;

        public void Configure(AutoplayConfig cfg)
        {
            _framesDir = Path.Combine(cfg.OutDir, "frames");
            _eventsDir = Path.Combine(cfg.OutDir, "events");
            _eventsPath = Path.Combine(cfg.OutDir, "events.jsonl");
            _interval = Mathf.Max(0.02f, cfg.Interval);

            // Telemetry truncates its file per run, and the manifest has to agree:
            // two runs sharing one output directory should read as the later one
            // rather than as a run that photographed the same moment twice.
            if (AutoplayCaptureTriggers.Any)
            {
                Directory.CreateDirectory(cfg.OutDir);
                File.WriteAllText(_eventsPath, string.Empty);
            }
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

        private IEnumerator CaptureLoop() => CaptureLoop(ScreenCapture.CaptureScreenshot);

        internal IEnumerator CaptureLoop(System.Action<string> capture)
        {
            float acc = 0f;
            bool first = true;
            while (true)
            {
                yield return new WaitForEndOfFrame();
                float delta = AutoplayTimeControl.GameDelta;
                AutoplayCaptureTriggers.Tick(delta);

                // A trigger fired for a reason, so it takes the frame; the interval
                // capture keeps its accumulator and lands on the next one.
                if (AutoplayCaptureTriggers.TryTakeReady(out var request))
                {
                    CaptureEvent(capture, request);
                    continue;
                }

                acc += delta;
                if (first || acc >= _interval)
                {
                    first = false;
                    acc = 0f;
                    capture(Path.Combine(_framesDir, $"frame_{_index:D5}.png"));
                    _index++;
                }
            }
        }

        /// <summary>
        /// Photographs one fired trigger and appends its manifest line, so a reader
        /// knows what each event picture is of without decoding the file name.
        /// </summary>
        private void CaptureEvent(
            System.Action<string> capture,
            AutoplayCaptureTriggers.Request request
        )
        {
            Directory.CreateDirectory(_eventsDir);
            string name = $"{request.Event}-{request.Occurrence:D3}.png";
            capture(Path.Combine(_eventsDir, name));
            File.AppendAllText(
                _eventsPath,
                AutoplayCaptureTriggers.Record(request, "events/" + name) + "\n"
            );
        }
    }
}
