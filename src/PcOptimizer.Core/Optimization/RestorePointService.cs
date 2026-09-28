using PcOptimizer.Core.Platform;

namespace PcOptimizer.Core.Optimization;

public interface IRestorePointService
{
    /// <summary>Tenta criar um ponto de restauração do Windows. Não lança exceção.</summary>
    (bool Created, string Message) TryCreate(string description);
}

public sealed class WindowsRestorePointService(ICommandRunner commands) : IRestorePointService
{
    public (bool Created, string Message) TryCreate(string description)
    {
        // O Windows só permite um ponto a cada 24h por padrão e a Proteção do Sistema pode estar desligada.
        const string frequencyKey = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
        commands.Run("reg", $"add \"{frequencyKey}\" /v SystemRestorePointCreationFrequency /t REG_DWORD /d 0 /f");

        var script = "Enable-ComputerRestore -Drive \"$env:SystemDrive\\\" -ErrorAction SilentlyContinue; " +
            $"Checkpoint-Computer -Description '{description.Replace("'", "''")}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop";
        var result = commands.Run("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"", TimeSpan.FromMinutes(5));

        return result.Success
            ? (true, "Ponto de restauração do Windows criado.")
            : (false, "Não foi possível criar ponto de restauração do Windows (o backup próprio do app continua valendo).");
    }
}
