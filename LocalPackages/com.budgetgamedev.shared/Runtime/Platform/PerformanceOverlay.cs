using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using BudgetGameDev.Shared.Rendering;

namespace BudgetGameDev.Shared
{
    public sealed class PerformanceOverlay : MonoBehaviour
    {
        private const string Preference = "Display.PerformanceOverlay";
        private readonly FrameStatistics statistics = new();
        private readonly FrameGenerationStatistics presentation = new();
        private Canvas canvas;
        private TMP_Text text;
        private PerformanceFrameGraph graph;
        private PerformanceResources resources;
        private double previous,
            refreshAt;
        public static bool Visible
        {
            get => PlayerPrefs.GetInt(Preference, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(Preference, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
        public static string ToggleLabel => "PERFORMANCE OVERLAY: " + (Visible ? "ON" : "OFF");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (!NativePlayerPerformance.IsDesktopPlayer(Application.platform))
                return;
            var host = new GameObject("Performance overlay", typeof(RectTransform));
            DontDestroyOnLoad(host);
            host.AddComponent<PerformanceOverlay>();
        }

        private void Awake()
        {
            resources = new PerformanceResources(SystemInfo.graphicsDeviceName, Application.dataPath);
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var panel = MenuTheme.CreatePanel(
                "Frame statistics",
                (RectTransform)transform,
                new Color(.025f, .045f, .035f, .88f)
            );
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-20, -120);
            panel.sizeDelta = new Vector2(490, 660);
            text = MenuTheme.CreateText(
                "Statistics",
                panel,
                "Measuring frames…",
                17,
                Color.white,
                TMP_Settings.defaultFontAsset
            );
            text.fontStyle = FontStyles.Normal;
            text.richText = true;
            text.characterSpacing = 0;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12, 80);
            text.rectTransform.offsetMax = new Vector2(-12, -10);
            var graphRect = MenuTheme.CreateRect("Frame-time graph", panel);
            graphRect.anchorMin = Vector2.zero;
            graphRect.anchorMax = new Vector2(1, 0);
            graphRect.pivot = Vector2.zero;
            graphRect.anchoredPosition = new Vector2(12, 10);
            graphRect.sizeDelta = new Vector2(-24, 62);
            graph = graphRect.gameObject.AddComponent<PerformanceFrameGraph>();
            graph.raycastTarget = false;
            previous = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double elapsed = now - previous;
            previous = now;
            canvas.enabled = Visible;
            if (!Application.isFocused)
                return;
            statistics.Add(now, elapsed);
            graph.AddFrame(now, (float)(elapsed * 1000), Visible);
            if (!Visible || now < refreshAt)
                return;
            refreshAt = now + .25;
            statistics.Calculate();
            bool native = StreamlineNative.TryGetStatus(out var status);
            var diagnostics = default(StreamlineNative.Diagnostics);
            bool telemetry = native && StreamlineNative.TryGetDiagnostics(out diagnostics);
            presentation.Add(StreamlineSettings.GeneratedFrames, native, telemetry, status, diagnostics);
            double renderedFps = presentation.RenderedFps ?? statistics.Fps;
            var sample = resources.Latest;
            bool fresh = sample.Fresh;
            double? videoPressure = fresh && sample.VideoTotal > 0
                ? sample.SystemVideoMemory / sample.VideoTotal * 100 : null;
            double? ramPressure = fresh && sample.RamTotal > 0
                ? sample.RamUsed / sample.RamTotal * 100 : null;
            double? diskPressure = fresh && sample.DiskSpaceTotal > 0
                ? sample.DiskSpaceUsed / sample.DiskSpaceTotal * 100 : null;
            string cap =
                Application.targetFrameRate < 0 ? "Uncapped" : $"Cap {Application.targetFrameRate}";
            text.text =
                $"<b>RENDERED {PerformanceTint.Fps(renderedFps)} FPS</b>   {PerformanceTint.Milliseconds(renderedFps > 0 ? 1000 / renderedFps : 0)} ms\n"
                + presentation.FormatRates()
                + PerformanceLatency.Format(telemetry, diagnostics)
                + $"1% LOW {PerformanceTint.Fps(statistics.OnePercentLowFps)} FPS   P99 {PerformanceTint.Milliseconds(statistics.P99Milliseconds)} ms\n"
                + $"{PerformanceLatency.Pipeline(GraphicsSettings.currentRenderPipeline?.GetType().Name)} · {Screen.currentResolution.refreshRateRatio.value:F0} Hz · VSync {(QualitySettings.vSyncCount == 0 ? "OFF" : "ON")} · {cap}\n"
                + $"CPU {Percent(fresh ? sample.SystemCpu : null)} system · {Percent(fresh ? sample.GameCpu : null)} game\n"
                + $"GPU {Percent(fresh ? sample.Gpu : null)} game\n"
                + $"CPU CLOCK {Clock(fresh ? sample.CpuClock : null)} MHz · {(sample.CpuClockSource.StartsWith("Windows") ? "OS reported" : "sensor")}\n"
                + $"CPU BOOST/PEAK {Clock(fresh ? sample.CpuBoostClock : null)} MHz · {(sample.CpuBoostSource == "Core sensor" ? "sensor" : "estimate")}\n"
                + $"GPU CLOCK {Clock(fresh ? sample.GpuClock : null)} · VRAM {Clock(fresh ? sample.GpuMemoryClock : null)} MHz\n"
                + $"RAM CFG {Clock(fresh ? sample.RamConfiguredClock : null)} MHz · {Clock(fresh ? sample.RamConfiguredRate : null)} MT/s\n"
                + (fresh ? sample.RamConfiguration?.OverlayLine ?? "" : "")
                + $"RAM LIVE {Clock(fresh ? sample.RamClock : null)} MHz\n"
                + $"VRAM {Percent(videoPressure)} · {GiB(fresh ? sample.SystemVideoMemory : null, videoPressure)}/{GiB(fresh ? sample.VideoTotal : null)} GiB system\n"
                + $"GAME VRAM {GiB(fresh ? sample.VideoMemory : null, videoPressure)} GiB · AVAILABLE {GiB(fresh ? sample.VideoAvailable : null, videoPressure)} GiB\n"
                + $"RAM {Percent(ramPressure)} · {GiB(fresh ? sample.RamUsed : null, ramPressure)}/{GiB(fresh ? sample.RamTotal : null)} GiB system\n"
                + $"GAME RAM {GiB(fresh ? sample.GameRam : null, ramPressure)} GiB\n"
                + $"DISK {Percent(fresh ? sample.DiskBusy : null)} busiest · R {Rate(fresh ? sample.DiskRead : null)} W {Rate(fresh ? sample.DiskWrite : null)} MiB/s\n"
                + $"DISK I/O {Clock(fresh ? sample.DiskTransfers : null)} /s · LAT {PerformanceTint.Format(fresh ? sample.DiskLatencyMs : null, "F1", PerformanceTint.Neutral)} ms (all disks)\n"
                + $"GAME DISK SPACE {Percent(diskPressure)} used\n"
                + $"{GiB(fresh ? sample.DiskSpaceUsed : null, diskPressure)}/{GiB(fresh ? sample.DiskSpaceTotal : null)} GiB\n"
                + $"TEMP · CPU {Temperature(fresh ? sample.CpuTemperature : null, 80, 95)} · GPU {Temperature(fresh ? sample.GpuTemperature : null, 80, 90)} · RAM {Temperature(fresh ? sample.RamTemperature : null, 70, 85)}\n"
                + $"TEMP · DISK {Temperature(fresh ? sample.DiskTemperature : null, 60, 70)} · BOARD {Temperature(fresh ? sample.BoardTemperature : null, 70, 85)}\n"
                + $"10 s frame time · 50 ms peaks · 0–{graph.ScaleMilliseconds:F0} ms";
        }

        private void OnApplicationFocus(bool focused)
        {
            statistics.Clear();
            presentation.Clear();
            previous = Time.realtimeSinceStartupAsDouble;
            graph?.Clear();
        }

        private static string Percent(double? value) =>
            PerformanceTint.Format(value, "F0", PerformanceTint.Pressure(value), "%");

        private static string Temperature(double? value, double warning, double bad) =>
            PerformanceTint.Format(value, "F0", PerformanceTint.High(value, warning, bad), "°C");

        private static string GiB(double? value, double? pressure = null) =>
            PerformanceTint.Format(value / (1024 * 1024 * 1024), "F1", PerformanceTint.Pressure(pressure));

        private static string Rate(double? value) =>
            PerformanceTint.Format(value / (1024 * 1024), "F1", PerformanceTint.Neutral);
        private static string Clock(double? value) => PerformanceTint.Format(value, "F0", PerformanceTint.Neutral);

        private void OnDestroy() => resources?.Dispose();
    }

    // Indicative display thresholds, not hardware-specific thermal limits or fault detection.
    internal static class PerformanceTint
    {
        internal const string Good = "#75E89D", Warning = "#FFD166", Bad = "#FF7373",
            Neutral = "#C7D6E0", Unknown = "#9BA7AE";
        internal const string Unavailable = "<color=" + Unknown + ">N/A</color>";

        internal static string High(double? value, double warning, double bad) =>
            !value.HasValue ? Neutral : value >= bad ? Bad : value >= warning ? Warning : Good;

        // Utilization indicates remaining headroom; saturation alone is not a hardware fault.
        internal static string Pressure(double? value) => High(value, 80, 95);

        internal static string Fps(double value) =>
            value > 0 ? Format(value, "F0", value >= 60 ? Good : value >= 30 ? Warning : Bad) : Unavailable;

        internal static string FrameColor(double milliseconds) =>
            milliseconds <= 1000d / 60 ? Good : milliseconds <= 1000d / 30 ? Warning : Bad;

        internal static string Milliseconds(double value) =>
            value > 0 ? Format(value, "F1", FrameColor(value)) : Unavailable;

        internal static string Format(double? value, string format, string tint, string suffix = "") =>
            !value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value)
                ? Unavailable
                : $"<color={tint}>{value.Value.ToString(format)}{suffix}</color>";
    }
}
