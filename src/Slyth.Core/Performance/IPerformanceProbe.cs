using Slyth.Core.Network;

namespace Slyth.Core.Performance;

/// <summary>Leituras do sistema usadas pelo teste de desempenho (implementação real só no Windows).</summary>
public interface IPerformanceProbe
{
    double? BootSeconds();

    double? RamUsedGb();

    Task<double?> IdleCpuPercentAsync(TimeSpan duration, CancellationToken cancel);

    int? Processes();

    int? RunningServices();

    int? StartupApps();

    Task<LatencyResult?> LatencyAsync(CancellationToken cancel);

    Task<double?> DnsMsAsync(CancellationToken cancel);

    double? DiskFreeGb();
}
