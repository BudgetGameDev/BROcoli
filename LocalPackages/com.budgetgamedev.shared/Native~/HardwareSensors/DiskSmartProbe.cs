using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using BudgetGameDev.Shared;
using Microsoft.Win32.SafeHandles;

internal static class DiskSmartProbe
{
    // Query-only handles and IOCTLs. Never enable SMART, write attributes or start self-tests.
    internal static DiskSmartReading[] Read(string gameDirectory)
    {
        var game = MapVolume(gameDirectory);
        var system = MapVolume(Environment.SystemDirectory);
        var result = new List<DiskSmartReading>();
        for (int index = 0; index < 64; index++)
        {
            using var disk = Open($@"\\.\PhysicalDrive{index}");
            int error = Marshal.GetLastWin32Error();
            if (disk.IsInvalid && (error == 2 || error == 3 || error == 15)) continue;
            var r = new DiskSmartReading
            {
                id = "PhysicalDrive" + index, model = "Model unavailable",
                scope = (game.Ids.Contains(index) ? "Game volume; " : "") + (system.Ids.Contains(index) ? "Windows volume; " : "")
                    + (game.Ids.Contains(index) || system.Ids.Contains(index) ? "" : "Other/unmapped drive; "),
                source = "Windows storage SMART queries", status = "Unavailable",
                detail = game.Error + system.Error,
            };
            result.Add(r);
            if (disk.IsInvalid) { r.detail += Describe("Open drive", error); r.sampledAt = Now(); continue; }
            var descriptor = new byte[1024];
            int bus = -1;
            if (Ioctl(disk, 0x2D1400, new byte[12], descriptor, out uint descriptorSize) && descriptorSize >= 36)
            {
                bus = (int)BitConverter.ToUInt32(descriptor, 28);
                int modelOffset = (int)BitConverter.ToUInt32(descriptor, 16);
                if (modelOffset > 0 && modelOffset < descriptorSize)
                {
                    int end = modelOffset;
                    while (end < Math.Min(descriptorSize, descriptor.Length) && descriptor[end] != 0) end++;
                    r.model = Encoding.ASCII.GetString(descriptor, modelOffset, end - modelOffset).Trim();
                }
            }
            // NVMe first, including bridges whose bus type does not reveal the underlying device.
            var query = new byte[560];
            Put(query, 0, 50); Put(query, 8, 3); Put(query, 12, 2); Put(query, 16, 2);
            Put(query, 24, 40); Put(query, 28, 512);
            var data = new byte[560];
            if (Ioctl(disk, 0x2D1400, query, data, out uint bytes))
            {
                try { DiskSmartHealth.DecodeNvme(data, (int)bytes, r); }
                catch (Exception e) { r.detail += "NVMe SMART: " + e.Message + " "; }
            }
            else r.detail += Describe("NVMe SMART", Marshal.GetLastWin32Error());
            if (!r.nvme)
            {
                var prediction = new byte[516];
                bool returnedPrediction = Ioctl(disk, 0x2D1100, Array.Empty<byte>(), prediction, out uint count);
                if (returnedPrediction && count >= 516)
                {
                    r.predictionKnown = true;
                    r.predictedFailure = BitConverter.ToUInt32(prediction, 0) != 0;
                    r.source = "Windows storage failure prediction (SMART where supported)";
                    r.status = "Available (summary only)";
                    r.rawData = Convert.ToBase64String(prediction, 4, 512);
                    r.detail += "Vendor-specific attributes are retained as raw data, not interpreted using universal thresholds. ";
                }
                else r.detail += returnedPrediction ? "Failure prediction: truncated driver response. " : Describe("Failure prediction", Marshal.GetLastWin32Error());
            }
            r.detail += "Storage bus type: " + bus + ".";
            r.sampledAt = Now();
        }
        if (result.Count == 0) result.Add(new DiskSmartReading { id = "Drive enumeration", scope = "Game/Windows mapping unavailable", status = "Unavailable", source = "Windows storage queries", detail = game.Error + system.Error + "No accessible physical drives were exposed.", sampledAt = Now() });
        return result.ToArray();
    }

    private static (HashSet<int> Ids, string Error) MapVolume(string directory)
    {
        var ids = new HashSet<int>();
        var mount = new StringBuilder(1024);
        var volume = new StringBuilder(1024);
        if (!GetVolumePathNameW(directory, mount, mount.Capacity) || !GetVolumeNameForVolumeMountPointW(mount.ToString(), volume, volume.Capacity))
            return (ids, Describe("Volume mapping", Marshal.GetLastWin32Error()));
        using var handle = Open(volume.ToString().TrimEnd('\\'));
        if (handle.IsInvalid) return (ids, Describe("Volume mapping", Marshal.GetLastWin32Error()));
        var data = new byte[4096];
        if (!Ioctl(handle, 0x560000, Array.Empty<byte>(), data, out uint length) || length < 8)
            return (ids, Describe("Volume extents", Marshal.GetLastWin32Error()));
        uint count = BitConverter.ToUInt32(data, 0);
        if (count > 128 || 8UL + 24UL * count > length) return (ids, "Invalid volume extents; mapping unavailable. ");
        for (int i = 0; i < count; i++) ids.Add((int)BitConverter.ToUInt32(data, 8 + 24 * i));
        return (ids, "");
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private static string Describe(string operation, int error) => operation + ": " + (error == 5 ? "Access denied" : "Unavailable") + $" (Windows {error}: {new Win32Exception(error).Message}). ";
    private static void Put(byte[] data, int offset, uint value) => BitConverter.GetBytes(value).CopyTo(data, offset);
    private static SafeFileHandle Open(string path) => CreateFileW(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
    private static bool Ioctl(SafeFileHandle handle, uint code, byte[] input, byte[] output, out uint returned) =>
        DeviceIoControl(handle, code, input, (uint)input.Length, output, (uint)output.Length, out returned, IntPtr.Zero);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, uint inputSize, byte[] output, uint outputSize, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathNameW(string path, StringBuilder volume, int length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPointW(string mount, StringBuilder volume, int length);
}
