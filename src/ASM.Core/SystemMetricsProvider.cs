using System.Runtime.InteropServices;
using System.IO;

namespace ASM.Core;

public interface ISystemMetricsProvider
{
    Task<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}

public sealed class SystemMetricsProvider : ISystemMetricsProvider
{
    private readonly GpuMetricsProvider _gpuProvider = new();

    public async Task<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var cpuTask = ReadCpuAsync(cancellationToken);
        var gpuTask = Task.Run(() => _gpuProvider.CaptureAsync(cancellationToken), cancellationToken);
        var memory = ReadMemory();
        var (disks, errors) = ReadDisks();
        var cpu = await cpuTask.ConfigureAwait(false);
        var (gpus, gpuError) = await gpuTask.ConfigureAwait(false);
        return new SystemSnapshot(DateTimeOffset.Now, cpu, memory, disks, errors, gpus, gpuError);
    }

    private static async Task<MetricValue<double>> ReadCpuAsync(CancellationToken token)
    {
        if (!GetSystemTimes(out var idleBefore, out var kernelBefore, out var userBefore))
            return new(null, $"Error de Windows {Marshal.GetLastWin32Error()}");
        await Task.Delay(650, token).ConfigureAwait(false);
        if (!GetSystemTimes(out var idleAfter, out var kernelAfter, out var userAfter))
            return new(null, $"Error de Windows {Marshal.GetLastWin32Error()}");
        var value = MetricMath.CpuPercent(idleBefore.Value, kernelBefore.Value, userBefore.Value,
            idleAfter.Value, kernelAfter.Value, userAfter.Value);
        return value is double percent ? new(percent) : new(null, "muestra inicial o delta inválido");
    }

    private static MetricValue<MemoryMetric> ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status)) return new(null, $"Error de Windows {Marshal.GetLastWin32Error()}");
        try { return new(MetricMath.CreateMemory(status.TotalPhysical, status.AvailablePhysical)); }
        catch (ArgumentOutOfRangeException) { return new(null, "Windows devolvió valores de memoria incoherentes"); }
    }

    private static (List<DiskMetric> Disks, List<string> Errors) ReadDisks()
    {
        var disks = new List<DiskMetric>();
        var errors = new List<string>();
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            errors.Add($"enumeración de unidades ({ex.Message})");
            return (disks, errors);
        }

        foreach (var drive in drives)
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed) continue;
                if (!drive.IsReady) { errors.Add(drive.Name); continue; }
                var total = checked((ulong)drive.TotalSize);
                var free = checked((ulong)drive.TotalFreeSpace);
                if (free > total) { errors.Add(drive.Name); continue; }
                var used = total - free;
                string? label;
                try { label = drive.VolumeLabel; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { label = null; }
                disks.Add(new DiskMetric(drive.Name, label, total, used, free, MetricMath.UsedPercent(total, used)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OverflowException)
            {
                errors.Add(drive.Name);
            }
        }
        return (disks, errors);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime { public uint Low; public uint High; public readonly ulong Value => ((ulong)High << 32) | Low; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length; public uint MemoryLoad; public ulong TotalPhysical; public ulong AvailablePhysical;
        public ulong TotalPageFile; public ulong AvailablePageFile; public ulong TotalVirtual; public ulong AvailableVirtual; public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
