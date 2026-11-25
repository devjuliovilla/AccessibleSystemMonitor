using System.Diagnostics;
using System.ComponentModel;

namespace ASM.Core;

public sealed class ProcessMetricsProvider
{
    private readonly GpuMetricsProvider _gpuProvider = new();

    public async Task<ProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default, bool includeGpu = true)
    {
        Process[] processHandles;
        try { processHandles = Process.GetProcesses(); }
        catch (Exception ex) when (IsExpectedProcessError(ex))
        {
            return new ProcessSnapshot(DateTimeOffset.Now, [], $"No se pudo enumerar procesos: {ex.Message}");
        }

        var candidates = new List<Candidate>(processHandles.Length);
        var errors = new List<string>();
        Task<GpuCaptureResult>? gpuTask = includeGpu
            ? Task.Run(() => _gpuProvider.CaptureAsync(cancellationToken, includeProcessMetrics: true), cancellationToken)
            : null;
        try
        {
            foreach (var process in processHandles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    process.Refresh();
                    var pid = process.Id;
                    var name = process.ProcessName;
                    TimeSpan? cpuBefore = null;
                    long? cpuTimestamp = null;
                    long? startTimeUtcTicks = null;
                    string? error = null;
                    try { startTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks; }
                    catch (Exception ex) when (IsExpectedProcessError(ex)) { error = ex.Message; }
                    try { cpuBefore = process.TotalProcessorTime; cpuTimestamp = Stopwatch.GetTimestamp(); }
                    catch (Exception ex) when (IsExpectedProcessError(ex)) { error = JoinError(error, ex.Message); }
                    candidates.Add(new Candidate(process, pid, name, cpuBefore, cpuTimestamp, startTimeUtcTicks, error));
                }
                catch (Exception ex) when (IsExpectedProcessError(ex))
                {
                    errors.Add($"un proceso ({ex.Message})");
                    process.Dispose();
                }
            }

            await Task.Delay(650, cancellationToken).ConfigureAwait(false);

            var values = new List<(Candidate Candidate, ulong? WorkingSet, double? Cpu, string? Error)>();
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    candidate.Process.Refresh();
                    ulong? workingSet = ReadBytes(() => candidate.Process.WorkingSet64);
                    double? cpu = null;
                    var error = candidate.Error;
                    if (candidate.CpuBefore is TimeSpan cpuBefore)
                    {
                        try
                        {
                            var currentCpu = candidate.Process.TotalProcessorTime;
                            var elapsed = candidate.CpuTimestamp is long timestamp ? Stopwatch.GetElapsedTime(timestamp) : TimeSpan.Zero;
                            cpu = MetricMath.ProcessCpuPercent(cpuBefore, currentCpu, elapsed, Environment.ProcessorCount);
                        }
                        catch (Exception ex) when (IsExpectedProcessError(ex)) { error = JoinError(error, ex.Message); }
                    }
                    if (workingSet is null && cpu is null)
                        error = JoinError(error, "Windows no permitió consultar las métricas de este proceso");
                    values.Add((candidate, workingSet, cpu, error));
                }
                catch (Exception ex) when (IsExpectedProcessError(ex))
                {
                    errors.Add($"PID {candidate.ProcessId} ({ex.Message})");
                }
            }

            var gpuCapture = gpuTask is null
                ? new GpuCaptureResult([], [], null)
                : await gpuTask.ConfigureAwait(false);
            var gpuByPid = gpuCapture.Processes.GroupBy(gpu => gpu.ProcessId).ToDictionary(group => group.Key, group => (IReadOnlyList<ProcessGpuMetric>)group.ToArray());
            var processMetrics = values.Select(value =>
            {
                gpuByPid.TryGetValue(value.Candidate.ProcessId, out var gpus);
                return new ProcessMetric(value.Candidate.ProcessId, value.Candidate.Name, value.WorkingSet,
                    value.Cpu, gpus ?? [], value.Error, value.Candidate.StartTimeUtcTicks);
            }).OrderByDescending(process => process.WorkingSetBytes ?? 0).ToArray();

            var errorSummary = new List<string>();
            if (errors.Count > 0) errorSummary.Add($"No se pudieron consultar {errors.Count} procesos");
            if (!string.IsNullOrWhiteSpace(gpuCapture.Error)) errorSummary.Add($"GPU: {gpuCapture.Error}");
            return new ProcessSnapshot(DateTimeOffset.Now, processMetrics, errorSummary.Count == 0 ? null : string.Join(". ", errorSummary));
        }
        finally
        {
            foreach (var process in processHandles) process.Dispose();
        }
    }

    private static ulong? ReadBytes(Func<long> read)
    {
        try
        {
            var bytes = read();
            return bytes >= 0 ? (ulong)bytes : null;
        }
        catch (Exception ex) when (IsExpectedProcessError(ex)) { return null; }
    }

    private static bool IsExpectedProcessError(Exception ex) => ex is InvalidOperationException or Win32Exception or NotSupportedException or UnauthorizedAccessException or System.Security.SecurityException;
    private static string JoinError(string? left, string right) => string.IsNullOrWhiteSpace(left) ? right : $"{left}; {right}";

    private sealed record Candidate(Process Process, int ProcessId, string Name, TimeSpan? CpuBefore, long? CpuTimestamp, long? StartTimeUtcTicks, string? Error);
}
