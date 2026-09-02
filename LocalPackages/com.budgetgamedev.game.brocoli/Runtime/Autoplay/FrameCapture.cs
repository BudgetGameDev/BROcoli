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
        /// <summary>
        /// Interval frames one run may write. The pictures are a flipbook, not a
        /// film: a twenty-minute coverage sweep at one frame a game-second is 1200
        /// full-screen PNGs, which is most of a gigabyte per run and far more
        /// pictures than anybody pages through. The budget buys the same coverage
        /// of the session with a number of frames a person can actually read.
        /// </summary>
        public const int DefaultMaxFrames = 120;

        private string _framesDir;
        private string _eventsDir;
        private string _eventsPath;
        private float _interval = 0.5f;
        private int _budget = DefaultMaxFrames;
        private int _index;

        public void Configure(AutoplayConfig cfg)
        {
            _framesDir = Path.Combine(cfg.OutDir, "frames");
            _eventsDir = Path.Combine(cfg.OutDir, "events");
            _eventsPath = Path.Combine(cfg.OutDir, "events.jsonl");
            _budget = Mathf.Max(1, cfg.MaxFrames);
            _interval = SpacedInterval(cfg.Interval, cfg.Duration, _budget);

            // Telemetry truncates its file per run, and the manifest has to agree:
            // two runs sharing one output directory should read as the later one
            // rather than as a run that photographed the same moment twice.
            if (AutoplayCaptureTriggers.Any)
            {
                Directory.CreateDirectory(cfg.OutDir);
                File.WriteAllText(_eventsPath, string.Empty);
            }
        }

        /// <summary>
        /// The interval that spreads <paramref name="budget"/> pictures over a run
        /// of <paramref name="duration"/> game-seconds, never finer than the tier
        /// asked for. Coarsening rather than truncating is the point: a run held to
        /// its budget still photographs its own ending, which is where a session
        /// that went wrong shows it.
        /// </summary>
        internal static float SpacedInterval(float interval, float duration, int budget)
        {
            float requested = Mathf.Max(0.02f, interval);
            if (duration <= 0f || budget <= 1)
                return requested;
            return Mathf.Max(requested, duration / (budget - 1));
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
                if (!first && acc < _interval)
                    continue;

                first = false;
                acc = 0f;

                // A run that outlives the duration the spacing was computed for --
                // a marathon that keeps going, a tier driven past its preset --
                // would otherwise walk straight past its budget.
                if (_index >= _budget)
                    continue;

                capture(Path.Combine(_framesDir, $"frame_{_index:D5}.png"));
                _index++;
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
