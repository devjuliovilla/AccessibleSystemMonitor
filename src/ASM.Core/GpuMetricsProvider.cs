using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ASM.Core;

public sealed class GpuMetricsProvider
{
    private const int PdhSuccess = 0;
    private const int PdhMoreData = unchecked((int)0x800007D2);
    private const uint PdhDouble = 0x00000200;
    private const uint PdhLarge = 0x00000400;
    private const uint DxgiAdapterFlagSoftware = 2;
    private static readonly Regex LuidPattern = new(@"luid_(0x[0-9a-f]+)_(0x[0-9a-f]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EnginePattern = new(@"(phys_\d+_eng_\d+_engtype_[^\)]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<(IReadOnlyList<GpuMetric> Gpus, string? Error)> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var adapters = EnumerateAdapters();
        if (!OperatingSystem.IsWindows()) return ([], "Los contadores de GPU solo están disponibles en Windows.");
        if (adapters.Count == 0) return ([], "No se encontraron adaptadores de GPU físicos mediante DXGI.");

        try
        {
            using var query = new PdhQuery();
            var errors = new List<string>();
            var engineCounters = TryAddWildcardCounters(query.Handle, @"\GPU Engine(*)\Utilization Percentage", null, errors);
            var memoryCounters = new List<CounterEntry>();
            memoryCounters.AddRange(TryAddWildcardCounters(query.Handle, @"\GPU Adapter Memory(*)\Dedicated Usage", "Dedicated", errors));
            memoryCounters.AddRange(TryAddWildcardCounters(query.Handle, @"\GPU Adapter Memory(*)\Shared Usage", "Shared", errors));
            if (engineCounters.Count == 0 && memoryCounters.Count == 0)
                return (MetricMath.AggregateGpus(adapters, [], []), string.Join("; ", errors.DefaultIfEmpty("Windows no expone contadores GPU Engine/GPU Adapter Memory.")));

            CheckPdh(PdhCollectQueryData(query.Handle));
            await Task.Delay(550, cancellationToken).ConfigureAwait(false);
            CheckPdh(PdhCollectQueryData(query.Handle));

            var engines = new List<GpuEngineSample>();
            foreach (var counter in engineCounters)
            {
                if (!TryGetLuid(counter.Instance, out var luid)) continue;
                var engine = EnginePattern.Match(counter.Instance);
                if (!engine.Success || !TryGetFormattedValue(counter.Handle, PdhDouble, out var value) || !double.IsFinite(value)) continue;
                engines.Add(new GpuEngineSample(luid, engine.Groups[1].Value, Math.Clamp(value, 0, 100)));
            }

            var memories = new List<GpuMemorySample>();
            foreach (var counter in memoryCounters)
            {
                if (!TryGetLuid(counter.Instance, out var luid) || !TryGetFormattedValue(counter.Handle, PdhLarge, out var value) || value < 0) continue;
                memories.Add(new GpuMemorySample(luid, counter.MemoryType!, (ulong)value));
            }
            return (MetricMath.AggregateGpus(adapters, engines, memories), errors.Count == 0 ? null : string.Join("; ", errors));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or ExternalException)
        {
            return (MetricMath.AggregateGpus(adapters, [], []), $"Error al consultar contadores GPU: {ex.Message}");
        }
    }

    private static List<CounterEntry> TryAddWildcardCounters(IntPtr query, string englishPath, string? memoryType, List<string> errors)
    {
        try { return AddWildcardCounters(query, englishPath, memoryType); }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
            return [];
        }
    }

    private static List<CounterEntry> AddWildcardCounters(IntPtr query, string englishPath, string? memoryType = null)
    {
        IntPtr wildcard = IntPtr.Zero;
        var entries = new List<CounterEntry>();
        try
        {
            CheckPdh(PdhAddEnglishCounterW(query, englishPath, UIntPtr.Zero, out wildcard));
            var localizedPath = GetCounterPath(wildcard);
            CheckPdh(PdhRemoveCounter(wildcard));
            wildcard = IntPtr.Zero;
            foreach (var path in ExpandWildcard(localizedPath))
            {
                if (!TryExtractInstance(path, out var instance)) continue;
                var status = PdhAddCounterW(query, path, UIntPtr.Zero, out var counter);
                if (status == PdhSuccess) entries.Add(new CounterEntry(counter, instance, memoryType));
            }
            return entries;
        }
        catch
        {
            foreach (var entry in entries) PdhRemoveCounter(entry.Handle);
            throw;
        }
        finally
        {
            if (wildcard != IntPtr.Zero) PdhRemoveCounter(wildcard);
        }
    }

    private static string GetCounterPath(IntPtr counter)
    {
        uint size = 0;
        var status = PdhGetCounterInfoW(counter, false, ref size, IntPtr.Zero);
        if (status != PdhMoreData || size == 0) throw new InvalidOperationException($"PDH no pudo resolver la ruta inglesa (0x{status:X8}).");
        var buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            status = PdhGetCounterInfoW(counter, false, ref size, buffer);
            CheckPdh(status);
            // PDH_COUNTER_INFO_W.szFullPath is at offset 40 in the x64 structure.
            var fullPath = Marshal.ReadIntPtr(buffer, 40);
            return Marshal.PtrToStringUni(fullPath) ?? throw new InvalidOperationException("PDH devolvió una ruta vacía.");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static IReadOnlyList<string> ExpandWildcard(string path)
    {
        uint length = 0;
        var status = PdhExpandWildCardPathW(IntPtr.Zero, path, IntPtr.Zero, ref length, 0);
        if (status != PdhMoreData || length == 0) return [];
        var buffer = Marshal.AllocHGlobal(checked((int)length * sizeof(char)));
        try
        {
            status = PdhExpandWildCardPathW(IntPtr.Zero, path, buffer, ref length, 0);
            CheckPdh(status);
            var paths = new List<string>();
            var current = new StringBuilder();
            for (var i = 0; i < length; i++)
            {
                var c = (char)Marshal.ReadInt16(buffer, checked((int)i * sizeof(char)));
                if (c == '\0')
                {
                    if (current.Length == 0) break;
                    paths.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(c);
            }
            return paths;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static bool TryExtractInstance(string path, out string instance)
    {
        var open = path.IndexOf('(');
        var close = path.IndexOf(')', open + 1);
        if (open < 0 || close <= open) { instance = string.Empty; return false; }
        instance = path[(open + 1)..close];
        return true;
    }

    private static bool TryGetFormattedValue(IntPtr counter, uint format, out long value)
    {
        var status = PdhGetFormattedCounterValue(counter, format, out _, out var formatted);
        if (status == PdhSuccess && formatted.Status is 0 or 1)
        {
            value = format == PdhDouble ? (long)Math.Round(formatted.DoubleValue) : formatted.LargeValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetLuid(string instance, out string luid)
    {
        var match = LuidPattern.Match(instance);
        if (!match.Success) { luid = string.Empty; return false; }
        luid = NormalizeLuid(match.Groups[1].Value, match.Groups[2].Value);
        return true;
    }

    private static string NormalizeLuid(string high, string low) => $"{Convert.ToUInt32(high[2..], 16):X8}_{Convert.ToUInt32(low[2..], 16):X8}";

    private static List<GpuAdapterInfo> EnumerateAdapters()
    {
        var adapters = new List<GpuAdapterInfo>();
        if (!OperatingSystem.IsWindows()) return adapters;
        var factoryId = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
        if (CreateDXGIFactory1(ref factoryId, out var factory) < 0 || factory == IntPtr.Zero) return adapters;
        try
        {
            var method = Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory), 12 * IntPtr.Size);
            var enumerate = Marshal.GetDelegateForFunctionPointer<EnumAdapters1Delegate>(method);
            for (uint index = 0; ; index++)
            {
                var result = enumerate(factory, index, out var adapter);
                if (result == unchecked((int)0x887A0002)) break; // DXGI_ERROR_NOT_FOUND
                if (result < 0 || adapter == IntPtr.Zero) break;
                try
                {
                    var getDescMethod = Marshal.ReadIntPtr(Marshal.ReadIntPtr(adapter), 10 * IntPtr.Size);
                    var getDesc = Marshal.GetDelegateForFunctionPointer<GetDesc1Delegate>(getDescMethod);
                    if (getDesc(adapter, out var desc) >= 0 && (desc.Flags & DxgiAdapterFlagSoftware) == 0)
                    {
                        var luid = NormalizeLuid($"0x{unchecked((uint)desc.AdapterLuid.HighPart):X8}", $"0x{desc.AdapterLuid.LowPart:X8}");
                        var capacity = desc.DedicatedVideoMemory == UIntPtr.Zero ? null : (ulong?)desc.DedicatedVideoMemory.ToUInt64();
                        adapters.Add(new GpuAdapterInfo(luid, desc.Description.TrimEnd('\0'), capacity));
                    }
                }
                finally { Marshal.Release(adapter); }
            }
        }
        finally { Marshal.Release(factory); }
        return adapters;
    }

    private static void CheckPdh(int status)
    {
        if (status != PdhSuccess) throw new InvalidOperationException($"PDH devolvió 0x{status:X8}.");
    }

    private sealed record CounterEntry(IntPtr Handle, string Instance, string? MemoryType);

    private sealed class PdhQuery : IDisposable
    {
        public IntPtr Handle { get; }
        public PdhQuery()
        {
            CheckPdh(PdhOpenQueryW(IntPtr.Zero, UIntPtr.Zero, out var handle));
            Handle = handle;
        }
        public void Dispose() { if (Handle != IntPtr.Zero) PdhCloseQuery(Handle); }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PdhFormattedValue
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(8)] public double DoubleValue;
        [FieldOffset(8)] public long LargeValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DxgiAdapterDesc1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint VendorId, DeviceId, SubSysId, Revision;
        public UIntPtr DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public Luid AdapterLuid;
        public uint Flags;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumAdapters1Delegate(IntPtr factory, uint index, out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetDesc1Delegate(IntPtr adapter, out DxgiAdapterDesc1 desc);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern int PdhOpenQueryW(IntPtr source, UIntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern int PdhAddEnglishCounterW(IntPtr query, string path, UIntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern int PdhAddCounterW(IntPtr query, string path, UIntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll")] private static extern int PdhGetCounterInfoW(IntPtr counter, [MarshalAs(UnmanagedType.Bool)] bool explain, ref uint bufferSize, IntPtr buffer);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern int PdhExpandWildCardPathW(IntPtr source, string path, IntPtr expandedPathList, ref uint bufferSize, uint flags);
    [DllImport("pdh.dll")] private static extern int PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll")] private static extern int PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PdhFormattedValue value);
    [DllImport("pdh.dll")] private static extern int PdhRemoveCounter(IntPtr counter);
    [DllImport("pdh.dll")] private static extern int PdhCloseQuery(IntPtr query);
    [DllImport("dxgi.dll", ExactSpelling = true)] private static extern int CreateDXGIFactory1(ref Guid iid, out IntPtr factory);
}
