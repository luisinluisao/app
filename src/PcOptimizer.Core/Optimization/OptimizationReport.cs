namespace PcOptimizer.Core.Optimization;

public enum TweakStatus
{
    Applied,
    AlreadyApplied,
    Failed,
}

public sealed record TweakResult(string TweakId, string TweakName, TweakStatus Status, string? Detail);

public sealed record OptimizationReport(
    string? BackupId,
    bool RestorePointCreated,
    IReadOnlyList<TweakResult> Results,
    bool RestartRequired)
{
    public int AppliedCount => Results.Count(r => r.Status == TweakStatus.Applied);

    public int FailedCount => Results.Count(r => r.Status == TweakStatus.Failed);
}

public sealed record RevertReport(int Restored, IReadOnlyList<string> Errors);
