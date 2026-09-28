using Slyth.Core.Network;
using Slyth.Core.Performance;

namespace Slyth.Core.Tests;

public class PerformanceTests
{
    private static MetricComparison Metric(IReadOnlyList<MetricComparison> list, string key) => list.Single(m => m.Key == key);

    [Fact]
    public void Comparison_reports_gains_losses_and_ignores_noise()
    {
        var before = new PerformanceSnapshot { BootSeconds = 40, RamUsedGb = 6.0, Processes = 210, PingMs = 20, DiskFreeGb = 100, StartupApps = 12 };
        var after = new PerformanceSnapshot { BootSeconds = 25, RamUsedGb = 5.9, Processes = 170, PingMs = 26, DiskFreeGb = 112.5, StartupApps = 4 };

        var result = PerformanceComparison.Compare(before, after);

        var boot = Metric(result, "boot");
        Assert.Equal(MetricVerdict.Better, boot.Verdict);
        Assert.Equal("−38%", boot.DeltaText);
        Assert.Equal(0.375, boot.Gain, 3);
        Assert.Equal(MetricVerdict.Same, Metric(result, "ram").Verdict);
        Assert.Equal("−40", Metric(result, "processes").DeltaText);
        Assert.Equal(MetricVerdict.Worse, Metric(result, "ping").Verdict);
        Assert.Equal("+12,5 GB", Metric(result, "disk").DeltaText);
        Assert.Equal(MetricVerdict.Better, Metric(result, "disk").Verdict);
        Assert.Equal(MetricVerdict.Unknown, Metric(result, "dns").Verdict);
        Assert.Equal("12", Metric(result, "startup").BeforeText);
    }

    [Fact]
    public void Boot_time_is_read_from_the_windows_event_and_ignores_previous_boots()
    {
        const string xml = "<Event><System><TimeCreated SystemTime='2026-09-28T10:05:00.0000000Z'/></System>" +
                           "<EventData><Data Name='BootTime'>23456</Data></EventData></Event>";

        Assert.Equal(23.456, BootTime.Parse(xml, new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc)));
        Assert.Null(BootTime.Parse(xml, new DateTime(2026, 9, 28, 11, 0, 0, DateTimeKind.Utc)));
        Assert.Null(BootTime.Parse("", DateTime.UtcNow));
    }

    [Fact]
    public async Task Benchmark_collects_every_metric_and_survives_failing_readings()
    {
        var snapshot = await PerformanceBenchmark.RunAsync(new FakeProbe(), "Antes", 42);

        Assert.Equal("Antes", snapshot.Label);
        Assert.Equal(31.5, snapshot.BootSeconds);
        Assert.Null(snapshot.RunningServices);
        Assert.Equal(3, snapshot.IdleCpuPercent);
        Assert.Equal(18, snapshot.PingMs);
        Assert.Equal(42, snapshot.Score);
    }

    [Fact]
    public void Store_keeps_order_and_reset_keeps_only_latest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "slyth-bench-" + Guid.NewGuid());
        try
        {
            var store = new SnapshotStore(dir);
            store.Save(new PerformanceSnapshot { TakenAt = new DateTime(2026, 1, 1), Label = "a" });
            store.Save(new PerformanceSnapshot { TakenAt = new DateTime(2026, 2, 1), Label = "b" });
            Assert.Equal(["a", "b"], store.LoadAll().Select(s => s.Label));

            store.ResetBaseline();
            Assert.Equal(["b"], store.LoadAll().Select(s => s.Label));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class FakeProbe : IPerformanceProbe
    {
        public double? BootSeconds() => 31.5;

        public double? RamUsedGb() => 7.2;

        public Task<double?> IdleCpuPercentAsync(TimeSpan duration, CancellationToken cancel) => Task.FromResult<double?>(3);

        public int? Processes() => 190;

        public int? RunningServices() => throw new InvalidOperationException("sem PowerShell");

        public int? StartupApps() => 9;

        public Task<LatencyResult?> LatencyAsync(CancellationToken cancel) =>
            Task.FromResult<LatencyResult?>(LatencyResult.From([18, 18, 18]));

        public Task<double?> DnsMsAsync(CancellationToken cancel) => Task.FromResult<double?>(12);

        public double? DiskFreeGb() => 250;
    }
}
