using ASM.Core;
using Xunit;

namespace ASM.Tests;

public sealed class MetricTests
{
    [Fact]
    public void CpuPercentSubtractsIdleFromKernelAndUserDelta()
    {
        Assert.Equal(75, MetricMath.CpuPercent(100, 200, 100, 150, 300, 200));
    }

    [Fact]
    public void CpuPercentReturnsUnavailableForZeroOrInvalidDelta()
    {
        Assert.Null(MetricMath.CpuPercent(10, 20, 30, 10, 20, 30));
        Assert.Null(MetricMath.CpuPercent(20, 20, 30, 10, 30, 40));
    }

    [Fact]
    public void MemoryUsesPhysicalTotalMinusAvailable()
    {
        var value = MetricMath.CreateMemory(1000, 250);
        Assert.Equal((ulong)750, value.UsedBytes);
        Assert.Equal(75, value.UsedPercent);
    }

    [Fact]
    public void MemoryRejectsAvailableGreaterThanTotal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MetricMath.CreateMemory(10, 11));
    }

    [Fact]
    public void FormatterKeepsZeroAndUnavailableDistinct()
    {
        var snapshot = new SystemSnapshot(DateTimeOffset.Now, new(0), new(null, "permiso denegado"), [], [], [], "contador GPU no disponible");
        var text = SnapshotFormatter.Format(snapshot);
        Assert.Contains("Procesador: 0 por ciento", text);
        Assert.Contains("Memoria RAM: No disponible (permiso denegado)", text);
        Assert.Contains("GPU: No disponible (contador GPU no disponible)", text);
    }

    [Fact]
    public void DiskPercentClampsInvalidUsedValue()
    {
        Assert.Equal(100, MetricMath.UsedPercent(10, 20));
        Assert.Equal(0, MetricMath.UsedPercent(0, 0));
    }

    [Fact]
    public void GpuAggregationSumsSameEngineInstancesButUsesBusiestEngine()
    {
        var adapters = new[] { new GpuAdapterInfo("A", "Adaptador A", 8UL * 1024 * 1024 * 1024) };
        var engines = new[]
        {
            new GpuEngineSample("A", "phys_0_eng_0_engtype_3D", 30),
            new GpuEngineSample("A", "phys_0_eng_0_engtype_3D", 40),
            new GpuEngineSample("A", "phys_0_eng_1_engtype_Copy", 60)
        };
        var memory = new[] { new GpuMemorySample("A", "Dedicated", 200), new GpuMemorySample("A", "Shared", 300) };
        var gpu = Assert.Single(MetricMath.AggregateGpus(adapters, engines, memory));
        Assert.Equal(70, gpu.UtilizationPercent);
        Assert.Equal((ulong)200, gpu.DedicatedUsedBytes);
        Assert.Equal((ulong)300, gpu.SharedUsedBytes);
        Assert.Equal((ulong)8 * 1024 * 1024 * 1024, gpu.DedicatedCapacityBytes);
    }

    [Fact]
    public void GpuAggregationDoesNotInventUtilizationWhenCountersAreMissing()
    {
        var gpu = Assert.Single(MetricMath.AggregateGpus([new("B", "Adaptador B", null)], [], []));
        Assert.Null(gpu.UtilizationPercent);
        Assert.Null(gpu.DedicatedUsedBytes);
        Assert.Contains("no están disponibles", gpu.Error);
    }

    [Fact]
    public async Task WindowsProviderReturnsSnapshotAndRepresentsUnavailableMetrics()
    {
        var snapshot = await new SystemMetricsProvider().CaptureAsync();
        Assert.NotEqual(default, snapshot.CapturedAt);
        Assert.True(snapshot.CpuPercent.IsAvailable || snapshot.CpuPercent.Error is not null);
        Assert.True(snapshot.Memory.IsAvailable || snapshot.Memory.Error is not null);
        Assert.All(snapshot.Disks, disk => Assert.InRange(disk.UsedPercent, 0, 100));
        Assert.True(snapshot.Gpus.Count > 0 || snapshot.GpuError is not null);
        Assert.All(snapshot.Gpus, gpu => Assert.True(gpu.UtilizationPercent.HasValue || gpu.DedicatedUsedBytes.HasValue || gpu.SharedUsedBytes.HasValue || gpu.Error is not null));
    }
}
