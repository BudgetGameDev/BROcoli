using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

// Additional conservative exclusion, not proof that no other driver accesses hardware.
// The pinned LHM library still owns its per-transaction bus locks. Never hold those
// locks around Open/Update: providers may use other threads and would deadlock.
internal sealed class SensorAccessGuard : IDisposable
{
    private readonly Func<string[]> scan;
    private readonly Func<string, Mutex> openMutex;
    private readonly List<Mutex> handles = [];
    private Mutex? lease;
    private bool ownsLease;
    public string? BlockReason { get; private set; }
    public string[] Detected { get; private set; } = [];

    // Names from LHM 0.9.6 Hardware/Mutexes.cs. Keep handles alive so its later
    // opens refer to the same objects. Refuse probing if FullControl is denied;
    // upstream otherwise permits access when a mutex could not be opened.
    internal static readonly string[] LibraryMutexNames =
    ["Global\\Access_ISABUS.HTP.Method", "Global\\Access_PCI", "Global\\Access_EC",
     "Global\\RazerReadWriteGuardMutex", "Global\\Access_USB_Sensors"];

    internal SensorAccessGuard(Func<string[]>? scan = null, Func<string, Mutex>? openMutex = null)
    {
        this.scan = scan ?? Scan;
        this.openMutex = openMutex ?? OpenSharedMutex;
    }

    public bool Check()
    {
        if (BlockReason != null) return false; // Latch until the player restarts.
        try
        {
            Detected = scan().Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
            if (Detected.Length != 0)
                BlockReason = "Other monitoring/tuning software detected: " + string.Join(", ", Detected) + ".";
        }
        catch (Exception e)
        {
            BlockReason = "Cannot check other monitoring processes: " + e.Message;
        }
        return BlockReason == null;
    }

    public bool EstablishCoordination()
    {
        if (!Check()) return false;
        try
        {
            lease = openMutex("Global\\BudgetGameDev.HardwareSensors.ProbeOwner.v1");
            try { ownsLease = lease.WaitOne(0); }
            catch (AbandonedMutexException) { ownsLease = true; }
            if (!ownsLease)
            {
                BlockReason = "Another game sensor reader already owns hardware probing.";
                return false;
            }
            foreach (string name in LibraryMutexNames) handles.Add(openMutex(name));
        }
        catch (Exception e)
        {
            BlockReason = "Cannot establish shared sensor locks: " + e.Message;
        }
        return BlockReason == null;
    }

    private static Mutex OpenSharedMutex(string name)
    {
        var security = new MutexSecurity();
        security.AddAccessRule(new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            MutexRights.FullControl, AccessControlType.Allow));
        // No fallback to private/session-only locks, and no unlocked fallback.
        return MutexAcl.Create(false, name, out _, security);
    }

    internal static bool IsMonitor(string name)
    {
        name = Path.GetFileName(name).ToLowerInvariant();
        if (name.EndsWith(".exe", StringComparison.Ordinal)) name = name[..^4];
        return new[] { "msiafterburner", "hwinfo", "hwmonitor", "openhardwaremonitor",
            "librehardwaremonitor", "fancontrol", "argusmonitor", "aida64", "aida32",
            "gpu-z", "gpuz", "cpuz", "cpu-z", "icue", "corsair", "armourycrate",
            "asusfancontrol", "asuscom", "asuscert", "aisuite", "aura", "lightingservice",
            "ryzenmaster", "amdryzenmaster", "nzxt", "cam_helper", "precisionx",
            "evgaprecision", "msicenter", "msi.central", "msi.center", "ledkeeper",
            "dragoncenter", "gigabytecontrol", "gcc", "gservice", "easytune",
            "siv", "aquasuite", "aquacomputer", "speedfan", "throttlestop", "xtuservice",
            "xtuui", "occt", "ohm", "lhm" }.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string[] Scan()
    {
        var matches = new List<string>();
        Process[] processes = Process.GetProcesses();
        try
        {
            foreach (Process process in processes)
            {
                try
                {
                    string name = process.ProcessName;
                    if (IsMonitor(name)) matches.Add(name);
                }
                catch (InvalidOperationException) { /* Process exited during enumeration. */ }
                // Access denied and other failures propagate; an incomplete scan must not permit probing.
            }
        }
        finally { foreach (Process process in processes) process.Dispose(); }
        return matches.ToArray();
    }

    public void Dispose()
    {
        foreach (Mutex handle in handles) handle.Dispose();
        handles.Clear();
        if (ownsLease) { lease!.ReleaseMutex(); ownsLease = false; }
        lease?.Dispose();
        lease = null;
    }
}
