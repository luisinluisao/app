namespace Slyth.Core.Performance;

/// <summary>Uma medição do PC. Campos null = não foi possível medir naquele momento.</summary>
public sealed record PerformanceSnapshot
{
    public DateTime TakenAt { get; init; } = DateTime.Now;

    public string Label { get; init; } = "";

    public double? BootSeconds { get; init; }

    public double? RamUsedGb { get; init; }

    public double? IdleCpuPercent { get; init; }

    public int? Processes { get; init; }

    public int? RunningServices { get; init; }

    public int? StartupApps { get; init; }

    public double? PingMs { get; init; }

    public double? JitterMs { get; init; }

    public double? DnsMs { get; init; }

    public double? DiskFreeGb { get; init; }

    public double? Score { get; init; }
}
