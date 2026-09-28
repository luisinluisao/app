using Slyth.Core.Platform;

namespace Slyth.Core.Tweaks;

/// <summary>Grava valores em cada adaptador de rede ativo (Tcpip\Parameters\Interfaces\{GUID}).</summary>
public sealed class NetworkInterfaceTweak : ITweak
{
    public const string InterfacesKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public TweakCategory Category => TweakCategory.Network;

    public TweakLevel Level { get; init; } = TweakLevel.Manual;

    public bool RequiresRestart { get; init; }

    public bool Reversible => true;

    public required IReadOnlyList<(string Name, RegistryValue Value)> Values { get; init; }

    public IReadOnlyList<(string File, string Args)> AfterApply { get; init; } = [];

    public bool IsApplied(TweakContext context)
    {
        var active = ActiveInterfaces(context.Registry);
        return active.Count > 0 && active.All(key =>
            Values.All(v => context.Registry.GetValue(RegistryRoot.LocalMachine, key, v.Name) == v.Value));
    }

    public string? Apply(TweakContext context)
    {
        var active = ActiveInterfaces(context.Registry);
        if (active.Count == 0)
        {
            throw new InvalidOperationException("Nenhum adaptador de rede conectado foi encontrado.");
        }

        foreach (var key in active)
        {
            foreach (var (name, value) in Values)
            {
                context.SetRegistry(RegistryRoot.LocalMachine, key, name, value);
            }
        }

        foreach (var (file, args) in AfterApply)
        {
            context.Commands.Run(file, args);
        }

        return $"{active.Count} adaptador(es) de rede";
    }

    /// <summary>Adaptadores com endereço IP (via DHCP ou fixo).</summary>
    public static IReadOnlyList<string> ActiveInterfaces(IRegistry registry) =>
        registry.GetSubKeyNames(RegistryRoot.LocalMachine, InterfacesKey)
            .Select(guid => $@"{InterfacesKey}\{guid}")
            .Where(key =>
            {
                var dhcp = registry.GetValue(RegistryRoot.LocalMachine, key, "DhcpIPAddress")?.Data;
                var fixedIp = registry.GetValue(RegistryRoot.LocalMachine, key, "IPAddress")?.Data;
                return IsAddress(dhcp) || IsAddress(fixedIp?.Split('\n').FirstOrDefault());
            })
            .ToList();

    private static bool IsAddress(string? ip) => !string.IsNullOrWhiteSpace(ip) && ip != "0.0.0.0";
}
