namespace PcOptimizer.Core.Platform;

public sealed record CommandResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;
}

/// <summary>Executa programas do sistema (powercfg, sc, powershell...).</summary>
public interface ICommandRunner
{
    CommandResult Run(string fileName, string arguments, TimeSpan? timeout = null);
}
