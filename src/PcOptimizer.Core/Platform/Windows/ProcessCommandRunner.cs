using System.Diagnostics;

namespace PcOptimizer.Core.Platform.Windows;

public sealed class ProcessCommandRunner : ICommandRunner
{
    public CommandResult Run(string fileName, string arguments, TimeSpan? timeout = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)(timeout ?? TimeSpan.FromMinutes(2)).TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            return new CommandResult(-1, stdout.Result, $"Tempo esgotado ao executar {fileName}.");
        }

        return new CommandResult(process.ExitCode, stdout.Result, stderr.Result);
    }
}
