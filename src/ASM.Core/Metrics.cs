namespace ASM.Core;

public sealed record MetricValue<T>(T? Value, string? Error = null) where T : struct
{
    public bool IsAvailable => Value.HasValue;
}

public readonly record struct MemoryMetric(ulong TotalBytes, ulong UsedBytes, ulong AvailableBytes, double UsedPercent);
public sealed record DiskMetric(string Name, string? VolumeLabel, ulong TotalBytes, ulong UsedBytes, ulong FreeBytes, double UsedPercent, string? Error = null);
public sealed record GpuMetric(string Luid, string Name, double? UtilizationPercent, ulong? DedicatedUsedBytes, ulong? DedicatedCapacityBytes, ulong? SharedUsedBytes, string? Error = null);
public sealed record GpuEngineSample(string Luid, string Engine, double UtilizationPercent);
public sealed record GpuMemorySample(string Luid, string MemoryType, ulong Bytes);
public sealed record GpuAdapterInfo(string Luid, string Name, ulong? DedicatedCapacityBytes);
public sealed record ProcessGpuEngineSample(int ProcessId, string Luid, string Engine, double UtilizationPercent);
public sealed record ProcessGpuMemorySample(int ProcessId, string Luid, string MemoryType, ulong Bytes);
public sealed record ProcessGpuMetric(int ProcessId, string Luid, string AdapterName, string? Engine, double? UtilizationPercent, ulong? DedicatedUsedBytes, ulong? SharedUsedBytes, string? Error = null);
public sealed record ProcessMetric(int ProcessId, string Name, ulong? WorkingSetBytes, double? CpuPercent, IReadOnlyList<ProcessGpuMetric> Gpus, string? Error = null, long? StartTimeUtcTicks = null);
public sealed record ProcessSnapshot(DateTimeOffset CapturedAt, IReadOnlyList<ProcessMetric> Processes, string? Error = null);
public sealed record GpuCaptureResult(IReadOnlyList<GpuMetric> Adapters, IReadOnlyList<ProcessGpuMetric> Processes, string? Error);
public sealed record SystemSnapshot(DateTimeOffset CapturedAt, MetricValue<double> CpuPercent, MetricValue<MemoryMetric> Memory, IReadOnlyList<DiskMetric> Disks, IReadOnlyList<string> DiskErrors, IReadOnlyList<GpuMetric> Gpus, string? GpuError = null);

public static class ProcessIdentity
{
    public static bool Matches(long? expectedStartTimeUtcTicks, long actualStartTimeUtcTicks) =>
        expectedStartTimeUtcTicks is > 0 && expectedStartTimeUtcTicks.Value == actualStartTimeUtcTicks;
}

public static class MetricMath
{
    public static double? CpuPercent(ulong previousIdle, ulong previousKernel, ulong previousUser, ulong idle, ulong kernel, ulong user)
    {
        if (idle < previousIdle || kernel < previousKernel || user < previousUser) return null;
        var idleDelta = idle - previousIdle;
        var kernelDelta = kernel - previousKernel;
        var userDelta = user - previousUser;
        if (ulong.MaxValue - kernelDelta < userDelta) return null;
        var total = kernelDelta + userDelta;
        if (total == 0 || idleDelta > total) return null;
        return Math.Clamp((double)(total - idleDelta) * 100.0 / total, 0.0, 100.0);
    }

    public static MemoryMetric CreateMemory(ulong total, ulong available)
    {
        if (available > total) throw new ArgumentOutOfRangeException(nameof(available));
        var used = total - available;
        var percent = total == 0 ? 0 : (double)used * 100.0 / total;
        return new MemoryMetric(total, used, available, percent);
    }

    public static double UsedPercent(ulong total, ulong used) => total == 0 ? 0 : Math.Clamp((double)used * 100.0 / total, 0.0, 100.0);

    public static double? ProcessCpuPercent(TimeSpan previous, TimeSpan current, TimeSpan elapsed, int processorCount)
    {
        if (processorCount <= 0 || elapsed <= TimeSpan.Zero || current < previous) return null;
        var percentage = (current - previous).TotalSeconds / elapsed.TotalSeconds / processorCount * 100.0;
        return double.IsFinite(percentage) ? Math.Clamp(percentage, 0, 100) : null;
    }

    public static IReadOnlyList<ProcessGpuMetric> AggregateProcessGpus(
        IEnumerable<ProcessGpuEngineSample> engines,
        IEnumerable<ProcessGpuMemorySample> memory,
        IEnumerable<GpuAdapterInfo> adapters)
    {
        var adapterNames = adapters.ToDictionary(a => a.Luid, a => a.Name, StringComparer.OrdinalIgnoreCase);
        var engineValues = engines.Where(e => e.ProcessId > 0 && double.IsFinite(e.UtilizationPercent))
            .GroupBy(e => (e.ProcessId, e.Luid, e.Engine), ProcessGpuKeyComparer.Instance)
            .Select(group => new ProcessGpuEngineSample(group.Key.ProcessId, group.Key.Luid, group.Key.Engine,
                Math.Clamp(group.Sum(x => x.UtilizationPercent), 0, 100)))
            .GroupBy(e => (e.ProcessId, e.Luid), ProcessGpuAdapterComparer.Instance)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.UtilizationPercent).First(), ProcessGpuAdapterComparer.Instance);
        var memoryValues = memory.Where(m => m.ProcessId > 0)
            .GroupBy(m => (m.ProcessId, m.Luid, m.MemoryType), ProcessGpuMemoryComparer.Instance)
            .ToDictionary(group => group.Key, group => SumBytes(group.Select(x => x.Bytes)), ProcessGpuMemoryComparer.Instance);
        var keys = engineValues.Keys.Concat(memoryValues.Keys.Select(k => (k.ProcessId, k.Luid))).Distinct(ProcessGpuAdapterComparer.Instance);

        return keys.Select(key =>
        {
            engineValues.TryGetValue(key, out var engine);
            memoryValues.TryGetValue((key.ProcessId, key.Luid, "Dedicated"), out var dedicated);
            memoryValues.TryGetValue((key.ProcessId, key.Luid, "Shared"), out var shared);
            adapterNames.TryGetValue(key.Luid, out var adapterName);
            return new ProcessGpuMetric(key.ProcessId, key.Luid, adapterName ?? $"Adaptador {key.Luid}", engine?.Engine,
                engine?.UtilizationPercent, dedicated, shared,
                engine is null && dedicated is null && shared is null ? "Contadores por proceso no disponibles" : null);
        }).OrderBy(x => x.ProcessId).ThenBy(x => x.AdapterName, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public static IReadOnlyList<GpuMetric> AggregateGpus(IEnumerable<GpuAdapterInfo> adapters, IEnumerable<GpuEngineSample> engines, IEnumerable<GpuMemorySample> memory)
    {
        var engineValues = engines.Where(e => double.IsFinite(e.UtilizationPercent))
            .GroupBy(e => (e.Luid, e.Engine), StringTupleComparer.Instance)
            .ToDictionary(g => g.Key, g => Math.Clamp(g.Sum(e => e.UtilizationPercent), 0, 100), StringTupleComparer.Instance);
        var engineByAdapter = engineValues.GroupBy(e => e.Key.Luid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (double?)g.Max(e => e.Value), StringComparer.OrdinalIgnoreCase);
        var memoryByAdapter = memory.GroupBy(m => m.Luid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new
            {
                Dedicated = SumBytes(g.Where(m => m.MemoryType.Equals("Dedicated", StringComparison.OrdinalIgnoreCase)).Select(m => m.Bytes)),
                Shared = SumBytes(g.Where(m => m.MemoryType.Equals("Shared", StringComparison.OrdinalIgnoreCase)).Select(m => m.Bytes))
            }, StringComparer.OrdinalIgnoreCase);

        return adapters.Select(adapter =>
        {
            engineByAdapter.TryGetValue(adapter.Luid, out var usage);
            memoryByAdapter.TryGetValue(adapter.Luid, out var mem);
            return new GpuMetric(adapter.Luid, adapter.Name, usage,
                mem?.Dedicated, adapter.DedicatedCapacityBytes, mem?.Shared,
                usage is null && mem is null ? "Los contadores del adaptador no están disponibles" : null);
        }).ToArray();
    }

    private static ulong? SumBytes(IEnumerable<ulong> values)
    {
        ulong sum = 0;
        bool found = false;
        foreach (var value in values)
        {
            found = true;
            if (ulong.MaxValue - sum < value) return null;
            sum += value;
        }
        return found ? sum : null;
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Luid, string Engine)>
    {
        public static readonly StringTupleComparer Instance = new();
        public bool Equals((string Luid, string Engine) x, (string Luid, string Engine) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Luid, y.Luid) && StringComparer.OrdinalIgnoreCase.Equals(x.Engine, y.Engine);
        public int GetHashCode((string Luid, string Engine) obj) => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Luid), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Engine));
    }

    private sealed class ProcessGpuKeyComparer : IEqualityComparer<(int ProcessId, string Luid, string Engine)>
    {
        public static readonly ProcessGpuKeyComparer Instance = new();
        public bool Equals((int ProcessId, string Luid, string Engine) x, (int ProcessId, string Luid, string Engine) y) =>
            x.ProcessId == y.ProcessId && StringComparer.OrdinalIgnoreCase.Equals(x.Luid, y.Luid) && StringComparer.OrdinalIgnoreCase.Equals(x.Engine, y.Engine);
        public int GetHashCode((int ProcessId, string Luid, string Engine) obj) => HashCode.Combine(obj.ProcessId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Luid), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Engine));
    }

    private sealed class ProcessGpuAdapterComparer : IEqualityComparer<(int ProcessId, string Luid)>
    {
        public static readonly ProcessGpuAdapterComparer Instance = new();
        public bool Equals((int ProcessId, string Luid) x, (int ProcessId, string Luid) y) => x.ProcessId == y.ProcessId && StringComparer.OrdinalIgnoreCase.Equals(x.Luid, y.Luid);
        public int GetHashCode((int ProcessId, string Luid) obj) => HashCode.Combine(obj.ProcessId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Luid));
    }

    private sealed class ProcessGpuMemoryComparer : IEqualityComparer<(int ProcessId, string Luid, string MemoryType)>
    {
        public static readonly ProcessGpuMemoryComparer Instance = new();
        public bool Equals((int ProcessId, string Luid, string MemoryType) x, (int ProcessId, string Luid, string MemoryType) y) =>
            x.ProcessId == y.ProcessId && StringComparer.OrdinalIgnoreCase.Equals(x.Luid, y.Luid) && StringComparer.OrdinalIgnoreCase.Equals(x.MemoryType, y.MemoryType);
        public int GetHashCode((int ProcessId, string Luid, string MemoryType) obj) => HashCode.Combine(obj.ProcessId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Luid), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.MemoryType));
    }
}

public static class SnapshotFormatter
{
    private static readonly System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.GetCultureInfo("es-MX");
    private const double GiB = 1024d * 1024 * 1024;

    public static string Format(SystemSnapshot snapshot)
    {
        var lines = new List<string> { $"Actualizado a las {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}." };
        lines.Add(snapshot.CpuPercent.Value is double cpu
            ? $"Procesador: {cpu.ToString("0", Culture)} por ciento de utilización."
            : $"Procesador: No disponible{ErrorSuffix(snapshot.CpuPercent.Error)}.");

        if (snapshot.Memory.Value is MemoryMetric memory)
            lines.Add($"Memoria RAM: {GiBValue(memory.UsedBytes)} GiB utilizados de {GiBValue(memory.TotalBytes)} GiB. {GiBValue(memory.AvailableBytes)} GiB disponibles. {memory.UsedPercent.ToString("0.0", Culture)} por ciento en uso.");
        else
            lines.Add($"Memoria RAM: No disponible{ErrorSuffix(snapshot.Memory.Error)}.");

        foreach (var disk in snapshot.Disks)
        {
            var label = string.IsNullOrWhiteSpace(disk.VolumeLabel) ? disk.Name : $"{disk.Name} ({disk.VolumeLabel})";
            lines.Add($"Disco {label}: {GiBValue(disk.FreeBytes)} GiB libres de {GiBValue(disk.TotalBytes)} GiB. {disk.UsedPercent.ToString("0.0", Culture)} por ciento utilizado. Actividad: No disponible.");
        }
        foreach (var error in snapshot.DiskErrors) lines.Add($"Almacenamiento: No disponible para {error}.");
        if (snapshot.Disks.Count == 0 && snapshot.DiskErrors.Count == 0) lines.Add("Almacenamiento: No hay unidades fijas disponibles.");
        foreach (var gpu in snapshot.Gpus)
        {
            var usage = gpu.UtilizationPercent is double percent ? $"{percent.ToString("0", Culture)} por ciento" : "No disponible";
            var dedicated = gpu.DedicatedUsedBytes is ulong used
                ? $"{GiBValue(used)} GiB utilizados" + (gpu.DedicatedCapacityBytes is ulong capacity ? $" de {GiBValue(capacity)} GiB" : string.Empty)
                : "No disponible";
            var shared = gpu.SharedUsedBytes is ulong sharedBytes ? $"{GiBValue(sharedBytes)} GiB" : "No disponible";
            lines.Add($"GPU {gpu.Name}: utilización {usage}. Memoria dedicada: {dedicated}. Memoria compartida: {shared}.{ErrorSuffix(gpu.Error)}");
        }
        if (snapshot.Gpus.Count == 0) lines.Add($"GPU: No disponible{ErrorSuffix(snapshot.GpuError)}.");
        else if (!string.IsNullOrWhiteSpace(snapshot.GpuError)) lines.Add($"GPU: Error parcial de contadores. {snapshot.GpuError}");
        lines.Add("RAM usada = total menos disponible; puede diferir del Administrador de tareas. Disco: espacio del volumen, no actividad.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string GiBValue(ulong bytes) => (bytes / GiB).ToString("0.0", Culture);
    private static string ErrorSuffix(string? error) => string.IsNullOrWhiteSpace(error) ? string.Empty : $" ({error})";
}
