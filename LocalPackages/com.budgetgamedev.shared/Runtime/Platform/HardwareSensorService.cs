using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BudgetGameDev.Shared
{
    /// <summary>Single read-only sensor sidecar per player, with bounded messages and stale-data handling.</summary>
    public sealed class HardwareSensorService : MonoBehaviour
    {
        [Serializable]
        public sealed class Reading
        {
            public string id, category, hardware, name, type, unit, status;
            public bool available;
            public float value;
        }
        [Serializable]
        public sealed class Notice { public string source, status, detail; }
        [Serializable]
        public sealed class Snapshot
        {
            public bool elevated, pawnIoInstalled;
            public string state;
            public Reading[] readings = Array.Empty<Reading>();
            public Notice[] notices = Array.Empty<Notice>();
            public DiskSmartReading[] diskSmart = Array.Empty<DiskSmartReading>();
            [NonSerialized] public long ReceivedAt;
            public bool Fresh => ReceivedAt != 0 &&
                (Stopwatch.GetTimestamp() - ReceivedAt) / (double)Stopwatch.Frequency < 10;
        }
        private static volatile Snapshot latest = new() { state = "Not started" };
        private static HardwareSensorService instance;
        private Process process;
        private string pending;
        private long started;
        private string failure;
        public static Snapshot Latest => latest;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            latest = new Snapshot { state = "Not started" };
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer || instance != null)
                return;
            var host = new GameObject("Hardware sensor discovery");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<HardwareSensorService>();
            instance.StartReader();
        }

        private void StartReader()
        {
            string path = Path.Combine(Application.dataPath, "StreamingAssets", "HardwareSensors", "HardwareSensors.exe");
            if (!File.Exists(path)) { failure = "Sensor reader is missing from this build."; return; }
            try
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo(path, "--parent " + Process.GetCurrentProcess().Id)
                    {
                        WorkingDirectory = Path.GetDirectoryName(path),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };
                process.StartInfo.EnvironmentVariables["BROCOLI_SENSOR_GAME_DIRECTORY"] = Application.dataPath;
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null && e.Data.Length <= 1024 * 1024 && e.Data.StartsWith("{"))
                        System.Threading.Interlocked.Exchange(ref pending, e.Data);
                };
                process.ErrorDataReceived += (_, e) => { /* Drain stderr; protocol reports errors as JSON. */ };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                started = Stopwatch.GetTimestamp();
            }
            catch (Exception e) { failure = "Sensor reader could not start: " + e.Message; StopReader(); }
        }

        private void Update()
        {
            string json = System.Threading.Interlocked.Exchange(ref pending, null);
            if (json != null)
            {
                try
                {
                    latest = ParseSnapshot(json);
                }
                catch (Exception e) { failure = "Sensor response was invalid: " + e.Message; }
            }
            if (process == null) return;
            long last = latest.ReceivedAt != 0 ? latest.ReceivedAt : started;
            if (process.HasExited)
            {
                failure = "Sensor reader exited. Available values are marked stale after 10 seconds.";
                StopReader();
            }
            else if ((Stopwatch.GetTimestamp() - last) / (double)Stopwatch.Frequency > 30)
            {
                failure = "Sensor discovery timed out. A provider or driver did not respond.";
                StopReader();
            }
        }

        internal static Snapshot ParseSnapshot(string json)
        {
            if (json == null || json.Length > 1024 * 1024)
                throw new InvalidDataException("Invalid sensor message size.");
            Snapshot parsed = JsonUtility.FromJson<Snapshot>(json);
            if (parsed == null || parsed.readings == null || parsed.readings.Length > 8192
                || (parsed.notices?.Length ?? 0) > 8192)
                throw new InvalidDataException("Invalid sensor message.");
            foreach (Reading reading in parsed.readings)
                if (reading != null && (float.IsNaN(reading.value) || float.IsInfinity(reading.value)
                    || (reading.type == "Temperature" && (reading.value <= 0 || reading.value >= 200))))
                {
                    reading.available = false;
                    reading.status = "Invalid reading";
                }
            parsed.ReceivedAt = Stopwatch.GetTimestamp();
            parsed.diskSmart ??= Array.Empty<DiskSmartReading>();
            if (parsed.diskSmart.Length > 128) throw new InvalidDataException("Too many SMART drive records.");
            return parsed;
        }

        public static double? PeakTemperature(string category) => PeakTemperature(latest, category);

        internal static double? PeakTemperature(Snapshot s, string category)
        {
            if (!s.Fresh) return null;
            double? result = null;
            foreach (Reading reading in s.readings)
            {
                if (reading == null || !reading.available || reading.type != "Temperature"
                    || !string.Equals(reading.category, category, StringComparison.OrdinalIgnoreCase)
                    || float.IsNaN(reading.value) || float.IsInfinity(reading.value)
                    || reading.value <= 0 || reading.value >= 200) continue;
                result = result.HasValue ? Math.Max(result.Value, reading.value) : reading.value;
            }
            return result;
        }

        public static string FormatReport() => FormatReport(latest, instance?.failure);

        internal static string FormatReport(Snapshot s, string failure = null)
        {
            var text = new StringBuilder("<b>DETECTED HARDWARE SENSORS</b>\n");
            text.Append("Process access: ").Append(s.ReceivedAt == 0 ? "Not reported" : s.elevated ? "Administrator" : "Standard user")
                .Append(" · PawnIO driver: ").Append(s.ReceivedAt == 0 ? "Not reported" : s.pawnIoInstalled ? "Installed" : "Not installed")
                .Append("\nProvider: Libre Hardware Monitor 0.9.6 · ").Append(Escape(s.state)).Append('\n');
            if (failure != null) text.Append(Escape(failure)).Append('\n');
            if (s.ReceivedAt != 0 && !s.Fresh) text.Append("<color=#FFD166>Readings are stale; no current temperature assessment is available.</color>\n");
            text.Append("Hardware probing pauses when known monitoring/tuning software is detected or shared sensor locks are unavailable. See access-coordination details below. A process scan cannot detect every competing driver.\n")
                .Append("Low-level CPU, DIMM and motherboard sensors may need administrator access and a compatible PawnIO driver. Elevation does not bypass the conflict guard or make concurrent probing safe. The game does not request voltage, clock or fan-control changes or install drivers.\n\n");
            int available = s.readings.Count(r => r != null && r.available);
            text.Append($"{available} available / {s.readings.Length} discovered readings · refresh every 2 s\n\n");
            text.Append(DiskSmartHealth.Format(s.diskSmart, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            string previous = null;
            foreach (Reading r in s.readings)
            {
                if (r == null) continue;
                string group = r.category + " · " + r.hardware;
                if (group != previous)
                {
                    text.Append("\n<b>").Append(Escape(group)).Append("</b>\n");
                    previous = group;
                }
                string value = !r.available ? Escape(r.status) : $"{r.value:0.##} {Escape(r.unit)}";
                string color = !r.available || !s.Fresh ? PerformanceTint.Unknown
                    : r.type == "Temperature" ? PerformanceTint.High(r.value, r.category == "Storage" ? 60 : 80, r.category == "Storage" ? 70 : 90)
                    : PerformanceTint.Neutral;
                text.Append(Escape(r.name)).Append(" [").Append(Escape(r.type)).Append("]: <color=")
                    .Append(color).Append('>').Append(value).Append("</color>")
                    .Append(r.available && !s.Fresh ? " (stale)" : "").Append('\n');
            }
            foreach (Notice notice in s.notices ?? Array.Empty<Notice>())
                if (notice != null)
                    text.Append("\n<color=#FFD166>").Append(Escape(notice.source)).Append(" · ")
                        .Append(Escape(notice.status)).Append("</color>\n").Append(Escape(notice.detail)).Append('\n');
            return text.ToString();
        }

        private static string Escape(string value) => (value ?? "").Replace("<", "‹").Replace(">", "›");

        private void StopReader()
        {
            if (process == null) return;
            try { if (!process.HasExited) process.Kill(); }
            catch (Exception e) { Debug.LogWarning("[HardwareSensors] " + e.Message); }
            process.Dispose();
            process = null;
        }
        private void OnDestroy() { StopReader(); if (instance == this) instance = null; }
        private void OnApplicationQuit() => StopReader();
    }
}
