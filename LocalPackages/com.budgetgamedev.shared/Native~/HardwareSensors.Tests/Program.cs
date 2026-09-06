if (args.Contains("--hold")) { Thread.Sleep(60000); return; }
int assertions = 0;
void Assert(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    assertions++;
}
foreach (string name in new[] { "MSIAfterburner.exe", "HWiNFO64", "FanControl", "iCUE",
    "ArmouryCrate.Service", "LibreHardwareMonitor", "MSI.CentralServer", "GPU-Z" })
    Assert(SensorAccessGuard.IsMonitor(name), "Recognize " + name);
foreach (string name in new[] { "BROcoli", "HardwareSensors", "explorer", "System", "notepad" })
    Assert(!SensorAccessGuard.IsMonitor(name), "Do not block " + name);

string[] competitors = [];
using (var guard = new SensorAccessGuard(() => competitors))
{
    Assert(guard.Check(), "Clean scan permits probing");
    competitors = ["MSIAfterburner"];
    Assert(!guard.Check() && guard.BlockReason!.Contains("MSIAfterburner"), "Late competitor pauses probing");
    competitors = [];
    Assert(!guard.Check(), "Pause latches after competitor exits");
}
using (var guard = new SensorAccessGuard(() => throw new UnauthorizedAccessException("test")))
    Assert(!guard.Check() && guard.BlockReason!.Contains("Cannot check"), "Incomplete scan fails closed");
using (var guard = new SensorAccessGuard(() => [], _ => throw new UnauthorizedAccessException("test lock access")))
    Assert(!guard.EstablishCoordination() && !guard.Check() && guard.BlockReason!.Contains("shared sensor locks"),
        "Lock access denied fails closed and latches");

using (var owner = new SensorAccessGuard(() => []))
{
    Assert(owner.EstablishCoordination(), "Can establish production global coordination objects");
    bool secondAllowed = true;
    var contender = new Thread(() =>
    {
        using var second = new SensorAccessGuard(() => []);
        secondAllowed = second.EstablishCoordination();
    });
    contender.Start();
    contender.Join();
    Assert(!secondAllowed, "Second thread cannot acquire probe ownership");
}
using (var after = new SensorAccessGuard(() => []))
    Assert(after.EstablishCoordination(), "Ownership released on disposal");
Console.WriteLine($"Passed {assertions} sensor guard assertions.");
