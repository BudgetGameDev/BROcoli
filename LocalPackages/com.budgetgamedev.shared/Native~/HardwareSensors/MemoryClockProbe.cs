using System.Management;

internal static class MemoryClockProbe
{
    internal static List<Reading> Read(List<Notice> notices)
    {
        var result = new List<Reading>();
        try
        {
            using var query = new ManagementObjectSearcher("SELECT DeviceLocator, Manufacturer, PartNumber, Speed, ConfiguredClockSpeed, SMBIOSMemoryType FROM Win32_PhysicalMemory");
            query.Options.Timeout = TimeSpan.FromSeconds(5);
            using var modules = query.Get();
            int index = 0;
            foreach (ManagementObject module in modules)
            using (module)
            {
                string hardware = module["DeviceLocator"]?.ToString() ?? "DIMM " + index;
                uint type = Convert.ToUInt32(module["SMBIOSMemoryType"] ?? 0);
                // DDR3/4/5 and LPDDR generations use two transfers per clock.
                if (type is not (24 or 26 or 27 or 28 or 29 or 30 or 34 or 35)) continue;
                string maker = module["Manufacturer"]?.ToString()?.Trim() ?? "";
                string part = module["PartNumber"]?.ToString()?.Trim() ?? "";
                hardware += (type == 34 ? " · DDR5" : type == 26 ? " · DDR4" : "")
                    + (maker.Length > 0 ? " · " + maker : "") + (part.Length > 0 ? " " + part : "");
                uint configured = Convert.ToUInt32(module["ConfiguredClockSpeed"] ?? 0);
                uint capability = Convert.ToUInt32(module["Speed"] ?? 0);
                void Add(string suffix, string name, string kind, string unit, float value) => result.Add(new Reading(
                    "/firmware/memory/" + index + "/" + suffix, "Memory", hardware, name, kind, unit,
                    value > 0 && value < 100000, value, value > 0 ? "Firmware configuration, not live measurement" : "Not exposed"));
                Add("configured", "Configured DDR rate", "DataRate", "MT/s", configured);
                Add("capability", "Firmware DDR capability", "DataRate", "MT/s", capability);
                Add("clock", "Configured DDR clock", "Clock", "MHz", configured / 2f);
                index++;
            }
        }
        catch (Exception e) { notices.Add(new Notice("Memory configuration", "Unavailable", e.Message)); }
        return result;
    }
}
