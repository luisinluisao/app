using Slyth.Core.Optimization;

namespace Slyth.Core.Performance;

/// <summary>Executa todas as medições em sequência (~20 s) e monta um <see cref="PerformanceSnapshot"/>.</summary>
public static class PerformanceBenchmark
{
    public static readonly TimeSpan CpuSampleDuration = TimeSpan.FromSeconds(5);

    public static async Task<PerformanceSnapshot> RunAsync(
        IPerformanceProbe probe, string label, double? score, IProgress<OptimizationProgress>? progress = null, CancellationToken cancel = default)
    {
        const int steps = 5;
        progress?.Report(new(0, steps, "Tempo de inicialização e memória"));
        var boot = Safe(probe.BootSeconds);
        var ram = Safe(probe.RamUsedGb);
        var processes = Safe(probe.Processes);
        var startup = Safe(probe.StartupApps);
        var disk = Safe(probe.DiskFreeGb);

        progress?.Report(new(1, steps, "Serviços em execução"));
        var services = await Task.Run(() => Safe(probe.RunningServices), cancel);

        progress?.Report(new(2, steps, "CPU em repouso (não mexa no PC)"));
        var cpu = await SafeAsync(() => probe.IdleCpuPercentAsync(CpuSampleDuration, cancel));

        progress?.Report(new(3, steps, "Ping e variação"));
        var latency = await SafeAsync(() => probe.LatencyAsync(cancel));

        progress?.Report(new(4, steps, "Velocidade do DNS"));
        var dns = await SafeAsync(() => probe.DnsMsAsync(cancel));

        progress?.Report(new(steps, steps, "Concluído"));
        var ok = latency is { LossPercent: < 100 };
        return new PerformanceSnapshot
        {
            Label = label,
            BootSeconds = boot,
            RamUsedGb = ram,
            IdleCpuPercent = cpu,
            Processes = processes,
            RunningServices = services,
            StartupApps = startup,
            PingMs = ok ? latency!.AverageMs : null,
            JitterMs = ok ? latency!.JitterMs : null,
            DnsMs = dns,
            DiskFreeGb = disk,
            Score = score,
        };
    }

    private static T? Safe<T>(Func<T?> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return default;
        }
    }

    private static async Task<T?> SafeAsync<T>(Func<Task<T?>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return default;
        }
    }
}
