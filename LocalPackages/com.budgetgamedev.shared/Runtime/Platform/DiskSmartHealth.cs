using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace BudgetGameDev.Shared
{
    [Serializable]
    public sealed class DiskSmartReading
    {
        public string id = "", model = "", scope = "", source = "", status = "", detail = "";
        public long sampledAt;
        public bool nvme, predictionKnown, predictedFailure;
        public int criticalWarning, spare, spareThreshold, percentageUsed;
        public string mediaErrors = "", errorLogEntries = "", unsafeShutdowns = "", powerOnHours = "", dataUnitsWritten = "";
        public string rawData = "";
    }

    /// <summary>Shared by the isolated Windows reader and Unity; no device I/O here.</summary>
    public static class DiskSmartHealth
    {
        public static void DecodeNvme(byte[] response, int length, DiskSmartReading result)
        {
            if (response == null || length < 48 || length > response.Length
                || BitConverter.ToUInt32(response, 0) != 48 || BitConverter.ToUInt32(response, 4) != 48
                || BitConverter.ToUInt32(response, 8) != 3 || BitConverter.ToUInt32(response, 12) != 2)
                throw new InvalidDataException("Invalid NVMe protocol descriptor.");
            uint offset = BitConverter.ToUInt32(response, 24), size = BitConverter.ToUInt32(response, 28);
            if (offset < 40 || size < 512 || 8UL + offset + size > (ulong)length)
                throw new InvalidDataException("Truncated NVMe SMART log.");
            int start = checked(8 + (int)offset);
            bool any = false;
            for (int i = 0; i < 512; i++) any |= response[start + i] != 0;
            if (!any || response[start + 3] > 100 || response[start + 4] > 100)
                throw new InvalidDataException("Empty or invalid NVMe SMART log.");
            result.criticalWarning = response[start];
            result.spare = response[start + 3];
            result.spareThreshold = response[start + 4];
            result.percentageUsed = response[start + 5];
            result.dataUnitsWritten = Counter(response, start + 48);
            result.powerOnHours = Counter(response, start + 128);
            result.unsafeShutdowns = Counter(response, start + 144);
            result.mediaErrors = Counter(response, start + 160);
            result.errorLogEntries = Counter(response, start + 176);
            result.rawData = Convert.ToBase64String(response, start, 512);
            result.nvme = true;
            result.source = "Windows NVMe SMART / Health Information Log";
            result.status = "Available";
        }

        private static string Counter(byte[] data, int offset)
        {
            var bytes = new byte[17]; // Unsigned little-endian 128-bit counter; preserve the entire value.
            Array.Copy(data, offset, bytes, 0, 16);
            return new BigInteger(bytes).ToString(CultureInfo.InvariantCulture);
        }

        public static bool Fresh(DiskSmartReading reading, long now) => reading != null
            && reading.sampledAt > 0 && now >= reading.sampledAt && now - reading.sampledAt <= 30;

        public static string Assess(DiskSmartReading r, long now)
        {
            if (!Fresh(r, now) || (!r.nvme && !r.predictionKnown)) return "NOT MEASURED";
            if (r.predictionKnown && r.predictedFailure) return "ATTENTION";
            if (r.nvme && (r.criticalWarning != 0 || r.spare < r.spareThreshold || r.percentageUsed >= 100)) return "ATTENTION";
            if (r.nvme && (r.percentageUsed >= 80 || Positive(r.mediaErrors))) return "CAUTION";
            return "NOMINAL";
        }

        private static bool Positive(string value) => BigInteger.TryParse(value, out BigInteger parsed) && parsed > 0;
        public static int Severity(string value) => value == "ATTENTION" ? 3 : value == "CAUTION" ? 2 : value == "NOMINAL" ? 1 : 0;
        private static string Safe(string text) => (text ?? "").Replace("<", "‹").Replace(">", "›");

        public static string Format(DiskSmartReading[] readings, long now, bool includeRaw = false, bool observedDuringTest = false)
        {
            var text = new StringBuilder("<b>DISK HEALTH · SMART</b>\n");
            if (readings == null || readings.Length == 0)
                return text.Append("<color=#9BA7AE>NOT MEASURED</color> · No fresh drive health data was exposed. Access, storage drivers or USB/RAID bridges may prevent SMART queries.\n\n").ToString();
            foreach (var r in readings)
            {
                if (r == null) continue;
                string assessment = Assess(r, observedDuringTest ? r.sampledAt : now);
                string color = assessment == "ATTENTION" ? "#FF7373" : assessment == "CAUTION" ? "#FFD166" : assessment == "NOMINAL" ? "#75E89D" : "#9BA7AE";
                text.Append("<b>").Append(Safe(r.id)).Append(" · ").Append(Safe(r.model)).Append(" · ")
                    .Append(Safe(r.scope)).Append(" · <color=").Append(color).Append('>').Append(assessment).Append("</color></b>\n")
                    .Append(Safe(r.source)).Append(" · ").Append(Safe(r.status)).Append('\n');
                if (observedDuringTest) text.Append("Fresh snapshot observed during the measured run; the worst observed status is retained.\n");
                else if (!Fresh(r, now)) text.Append("Cached reading is stale or unavailable; excluded from assessment.\n");
                if (r.nvme)
                {
                    text.Append($"Critical warning: 0x{r.criticalWarning:X2} ({Warnings(r.criticalWarning)}). Spare: {r.spare}% / threshold {r.spareThreshold}%.\n")
                        .Append($"Estimated endurance used: {r.percentageUsed}%. Media/data-integrity errors: {Safe(r.mediaErrors)}.\n")
                        .Append($"Error log entries: {Safe(r.errorLogEntries)}; unsafe shutdowns: {Safe(r.unsafeShutdowns)}; power-on hours: {Safe(r.powerOnHours)}.\n")
                        .Append($"Data units written (512,000 bytes each): {Safe(r.dataUnitsWritten)}.\n");
                }
                if (r.predictionKnown) text.Append("Driver predicts failure: ").Append(r.predictedFailure ? "YES" : "No").Append(". Attribute-level coverage may be unavailable.\n");
                if (!string.IsNullOrEmpty(r.detail)) text.Append(Safe(r.detail)).Append('\n');
                if (assessment == "ATTENTION")
                    text.Append("Back up important data promptly. Check the drive vendor's diagnostic tool and plan service/replacement if the warning is confirmed. For a temperature warning, check cooling. Freeing disk space does not repair SMART errors.\n");
                else if (assessment == "CAUTION")
                    text.Append("Keep a current backup and check the vendor's diagnostics. Historical media errors warrant investigation; they do not prove imminent failure. At high endurance usage, plan replacement.\n");
                else if (assessment == "NOMINAL")
                    text.Append("No warning in the available SMART fields; this does not guarantee drive health.\n");
                else
                    text.Append("Retry with administrator access if denied; unsupported controllers or bridges may still hide SMART. Missing data is not a healthy result.\n");
                if (includeRaw && !string.IsNullOrEmpty(r.rawData)) text.Append("Raw returned SMART payload (base64): ").Append(Safe(r.rawData)).Append('\n');
                text.Append('\n');
            }
            return text.Append("SMART counters are drive lifetime values, not errors caused by this benchmark. Error-log and unsafe-shutdown counts alone do not mark a drive unhealthy. Endurance is a vendor estimate; 80% is a caution heuristic, 100% is estimated rated endurance consumed, not proof of failure. Read-only queries; no drive self-test or repair is started.\n\n").ToString();
        }

        private static string Warnings(int mask)
        {
            if (mask == 0) return "none";
            var values = new System.Collections.Generic.List<string>();
            string[] names = { "spare below threshold", "temperature threshold", "reliability degraded", "read-only media", "volatile-memory backup failed", "persistent-memory region warning" };
            for (int bit = 0; bit < names.Length; bit++) if ((mask & (1 << bit)) != 0) values.Add(names[bit]);
            if ((mask & ~63) != 0) values.Add("additional device warning");
            return string.Join(", ", values);
        }
    }
}
