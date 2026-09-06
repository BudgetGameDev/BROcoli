using System;
using System.Collections.Generic;
using System.Text;

namespace BudgetGameDev.Shared
{
    internal sealed class MemoryConfiguration
    {
        private sealed class Module
        {
            internal string Name;
            internal double? Configured, Capability;
        }
        internal bool Suboptimal { get; private set; }
        internal string Details { get; private set; } = "Memory configuration unavailable.";
        internal string OverlayLine { get; private set; } = "";
        internal const string Advice = "Review the kit's rated specification and the CPU/motherboard memory support list. "
            + "If supported, review the matching XMP/EXPO profile in UEFI/BIOS; default settings or a disabled profile are possible causes, not confirmed here. "
            + "Mixed kits, populated slots and the memory controller can require a lower stable rate. "
            + "Validate stability after any supported change. This is a configuration opportunity, not a memory fault or an estimated FPS loss.";
        internal const string Limitations = "Firmware capability is not a verified advertised XMP/EXPO rating. "
            + "Profile contents and enabled state are not exposed by this check; matching firmware values do not prove the kit runs at its advertised speed. "
            + "Configured values are not live memory-clock or bandwidth measurements.";

        internal static MemoryConfiguration Assess(HardwareSensorService.Snapshot snapshot)
        {
            var result = new MemoryConfiguration();
            if (snapshot == null || !snapshot.Fresh) return result;
            var modules = new Dictionary<string, Module>();
            foreach (var r in snapshot.readings)
            {
                if (r == null || r.category != "Memory" || !r.available || r.type != "DataRate" || r.unit != "MT/s"
                    || r.value <= 0 || r.value >= 100000 || float.IsNaN(r.value) || float.IsInfinity(r.value)) continue;
                if (r.name != "Configured DDR rate" && r.name != "Firmware DDR capability") continue;
                // Match the same physical module; never compare global minimums from different DIMMs.
                string key = r.id != null && r.id.StartsWith("/firmware/memory/", StringComparison.Ordinal)
                    ? r.id.Substring(0, r.id.LastIndexOf('/')) : r.hardware;
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!modules.TryGetValue(key, out var module))
                    modules[key] = module = new Module { Name = string.IsNullOrWhiteSpace(r.hardware) ? key : r.hardware };
                if (r.name == "Configured DDR rate") module.Configured = r.value;
                else module.Capability = r.value;
            }
            var details = new StringBuilder();
            double worstRatio = 1;
            foreach (var module in modules.Values)
            {
                bool low = module.Configured.HasValue && module.Capability.HasValue && module.Configured < module.Capability * .95;
                result.Suboptimal |= low;
                details.Append(Escape(module.Name)).Append(": ")
                    .Append(module.Configured.HasValue ? $"{module.Configured:F0} MT/s configured" : "configured rate unavailable")
                    .Append("; ").Append(module.Capability.HasValue ? $"{module.Capability:F0} MT/s firmware capability" : "capability unavailable");
                if (low)
                {
                    double ratio = module.Configured.Value / module.Capability.Value;
                    details.Append($" — {(1 - ratio) * 100:F0}% below reported rate; SUBOPTIMAL CONFIGURATION");
                    if (ratio < worstRatio)
                    {
                        worstRatio = ratio;
                        result.OverlayLine = $"<color={PerformanceTint.Warning}>RAM CONFIG SUBOPTIMAL · {module.Configured:F0} / {module.Capability:F0} MT/s</color>\n";
                    }
                }
                details.AppendLine(".");
            }
            if (details.Length > 0) result.Details = details.ToString().TrimEnd();
            return result;
        }

        private static string Escape(string text) => (text ?? "").Replace("<", "‹").Replace(">", "›");
    }
}
