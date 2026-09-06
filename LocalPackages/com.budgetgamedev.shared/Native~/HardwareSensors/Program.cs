using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using BudgetGameDev.Shared;

// Separate runtime keeps vendor probing and a stalled driver off Unity's frame thread.
// No control setters, service installation, elevation request, or network communication.
bool elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
using var driverKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO");
bool pawnIo = driverKey != null;
int parentIndex = Array.IndexOf(args, "--parent");
Process? parent = parentIndex >= 0 && parentIndex + 1 < args.Length
    ? Process.GetProcessById(int.Parse(args[parentIndex + 1])) : null;
bool once = args.Contains("--once");
var notices = new List<Notice>();
var computer = new Computer();
using var sensorGuard = new SensorAccessGuard();
bool opened = false;
var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
var smart = Array.Empty<DiskSmartReading>();
long nextSmart = 0;
string gameDirectory = Environment.GetEnvironmentVariable("BROCOLI_SENSOR_GAME_DIRECTORY") ?? AppContext.BaseDirectory;
if (args.Contains("--smart-once"))
{
    Console.WriteLine(JsonSerializer.Serialize(new Snapshot(elevated, pawnIo, "Ready", [], [], DiskSmartProbe.Read(gameDirectory)), jsonOptions));
    return;
}
Console.WriteLine(JsonSerializer.Serialize(new Snapshot(elevated, pawnIo, "Discovering", [], [], smart), jsonOptions));
try
{
    if (sensorGuard.EstablishCoordination())
    {
        computer.Open();
        opened = true;
        Enable("Motherboard", () => computer.IsMotherboardEnabled = true);
        Enable("CPU", () => computer.IsCpuEnabled = true);
        Enable("Memory", () => computer.IsMemoryEnabled = true);
        Enable("GPU", () => computer.IsGpuEnabled = true);
        Enable("Storage", () => computer.IsStorageEnabled = true);
        Enable("Controllers", () => computer.IsControllerEnabled = true);
        Enable("Network", () => computer.IsNetworkEnabled = true);
        Enable("Battery", () => computer.IsBatteryEnabled = true);
        Enable("Power supply", () => computer.IsPsuEnabled = true);
        Enable("Power monitors", () => computer.IsPowerMonitorEnabled = true);
    }
    var memoryClocks = MemoryClockProbe.Read(notices);
    do
    {
        if (parent?.HasExited == true) break;
        var readings = new List<Reading>();
        readings.AddRange(memoryClocks);
        var issues = new List<Notice>(notices);
        bool probe = opened && sensorGuard.Check();
        if (Environment.TickCount64 >= nextSmart)
        {
            try { smart = DiskSmartProbe.Read(gameDirectory); }
            catch (Exception e) { issues.Add(Error("Disk SMART", e)); smart = []; }
            nextSmart = Environment.TickCount64 + 10000;
        }
        if (probe)
        foreach (IHardware hardware in computer.Hardware)
        {
            if (!sensorGuard.Check()) { probe = false; readings.Clear(); readings.AddRange(memoryClocks); break; }
            Read(hardware, hardware.HardwareType.ToString(), readings, issues);
        }
        issues.Add(new Notice("Sensor access coordination", probe ? "Guard active" : "Probing paused",
            (sensorGuard.BlockReason ?? "No known competing monitoring process detected; shared lock handles established.")
            + " Process-name detection is incomplete and shared locks require cooperating applications."
            + (probe ? "" : " Libre Hardware Monitor readings are withheld for this session. Windows/NVIDIA performance counters, firmware memory configuration and query-only SMART remain available. Restart the game after resolving the conflict to retry.")));
        if (probe)
        foreach (string category in new[] { "Motherboard", "Cpu", "Memory", "Gpu", "Storage", "Controller", "Network", "Battery", "Psu", "PowerMonitor" })
            if (!computer.Hardware.Any(h => h.HardwareType.ToString().StartsWith(category, StringComparison.Ordinal)))
                issues.Add(new Notice(category, "No hardware exposed", "Provider enabled, but no supported device was exposed with the current access and drivers."));
        Console.WriteLine(JsonSerializer.Serialize(new Snapshot(elevated, pawnIo, probe ? "Ready" : "Hardware probing paused", readings, issues, smart), jsonOptions));
        if (once) break;
        Thread.Sleep(2000);
    } while (true);
}
catch (Exception e)
{
    notices.Add(Error("Libre Hardware Monitor", e));
    Console.WriteLine(JsonSerializer.Serialize(new Snapshot(elevated, pawnIo, "Failed", [], notices, smart), jsonOptions));
}
finally { computer.Close(); parent?.Dispose(); }

void Enable(string name, Action action)
{
    if (!sensorGuard.Check()) return;
    try { action(); }
    catch (Exception e) { notices.Add(Error(name, e)); }
}

static Notice Error(string source, Exception e) => new(source,
    e is UnauthorizedAccessException || e.HResult == unchecked((int)0x80070005) ? "Access denied" : "Unavailable",
    e.Message);

static void Read(IHardware hardware, string category, List<Reading> readings, List<Notice> issues)
{
    bool updated = true;
    try { hardware.Update(); }
    catch (Exception e) { updated = false; issues.Add(Error(hardware.Name, e)); }
    foreach (ISensor sensor in hardware.Sensors)
    {
        bool valid = updated && sensor.Value.HasValue && float.IsFinite(sensor.Value.Value);
        bool invalidTemperature = valid && sensor.SensorType == SensorType.Temperature
            && (sensor.Value!.Value <= 0 || sensor.Value.Value >= 200);
        valid &= !invalidTemperature;
        readings.Add(new Reading(sensor.Identifier.ToString(), category, hardware.Name, sensor.Name,
            sensor.SensorType.ToString(), Unit(sensor.SensorType.ToString()), valid, valid ? sensor.Value!.Value : 0,
            valid ? "Available" : invalidTemperature ? "Invalid reading (zero or out of range)" : updated ? "Not exposed" : "Update failed"));
    }
    if (hardware.Sensors.Length == 0)
        issues.Add(new Notice(hardware.Name, "No sensors exposed", $"Detected {category}; no readings are available from this provider."));
    foreach (IHardware child in hardware.SubHardware)
        Read(child, category, readings, issues);
}

static string Unit(string type) => type switch
{
    "Temperature" => "°C", "Voltage" => "V", "Current" => "A", "Power" => "W",
    "Clock" => "MHz", "Frequency" => "Hz", "Fan" => "RPM", "Flow" => "L/h",
    "Load" or "Control" or "Level" or "Humidity" => "%", "Data" => "GiB",
    "SmallData" => "MiB", "Throughput" => "B/s", "Energy" => "mWh", "TimeSpan" => "s",
    "Timing" => "ns", "Noise" => "dBA", "Conductivity" => "µS/cm", "Factor" => "",
    _ => "",
};

record Reading(string id, string category, string hardware, string name, string type, string unit,
    bool available, float value, string status);
record Notice(string source, string status, string detail);
record Snapshot(bool elevated, bool pawnIoInstalled, string state, List<Reading> readings, List<Notice> notices, DiskSmartReading[] diskSmart);
