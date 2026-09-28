using Slyth.Core.Platform;

namespace Slyth.Core.Bios;

public static class FirmwareReboot
{
    /// <summary>Reinicia direto na tela da BIOS/UEFI (funciona em PCs com UEFI).</summary>
    public static CommandResult Restart(ICommandRunner commands) => commands.Run("shutdown", "/r /fw /t 5");
}
