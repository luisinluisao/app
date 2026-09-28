using Slyth.Core.Platform;
using Slyth.Core.Tweaks;

namespace Slyth.Core.Network;

public static class DnsTweaks
{
    /// <summary>
    /// Ajuste que troca o DNS (IPv4) de todos os adaptadores conectados. Passa pelo otimizador,
    /// então fica no Histórico e pode ser desfeito. null = voltar ao DNS automático da operadora.
    /// </summary>
    public static ITweak Create(DnsProvider? provider) => new NetworkInterfaceTweak
    {
        Id = provider is null ? "dns-automatic" : $"dns-{provider.Name.ToLowerInvariant()}",
        Name = provider is null ? "DNS automático (operadora)" : $"DNS {provider.Name}",
        Description = provider is null ? "Volta a usar o DNS entregue pelo roteador." : $"Usa {provider.Primary} e {provider.Secondary}.",
        RequiresRestart = true,
        Values = [("NameServer", RegistryValue.Str(provider?.NameServer ?? ""))],
        AfterApply = [("ipconfig", "/flushdns")],
    };
}
