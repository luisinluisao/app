using System.Text.RegularExpressions;
using Slyth.Core.Platform;

namespace Slyth.Core.Tweaks;

/// <summary>
/// Desliga a economia de energia das placas de rede físicas (Ethernet e Wi-Fi):
/// "Ethernet com eficiência energética", "Green Ethernet" e o desligamento do adaptador pelo Windows.
/// Essas economias causam picos de ping e quedas curtas de conexão. Só altera opções que o driver já oferece.
/// </summary>
public sealed partial class NetworkAdapterPowerTweak : ITweak
{
    public const string ClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    /// <summary>Palavras-chave de economia de energia usadas por drivers Intel, Realtek, Killer e o padrão NDIS.</summary>
    public static readonly IReadOnlyList<string> PowerSavingKeywords =
        ["*EEE", "EEELinkAdvertisement", "AdvancedEEE", "EnableGreenEthernet", "GreenEthernet", "PowerSavingMode", "*SelectiveSuspend", "ULPMode"];

    /// <summary>Valor documentado pela Microsoft que desmarca "Permitir que o computador desligue este dispositivo".</summary>
    private static readonly RegistryValue NoPowerManagement = RegistryValue.Dword(24);
    private static readonly RegistryValue Off = RegistryValue.Str("0");

    public string Id => "nic-power-saving-off";

    public string Name => "Placa de rede sem economia de energia";

    public string Description => "Desliga a \"Ethernet com eficiência energética\", o Green Ethernet e o desligamento automático da placa de rede. " +
        "Essas economias silenciosas causam picos de ping e microquedas.";

    public TweakCategory Category => TweakCategory.Network;

    public TweakLevel Level => TweakLevel.Gamer;

    public bool RequiresRestart => true;

    public bool Reversible => true;

    public bool IsApplied(TweakContext context)
    {
        var adapters = PhysicalAdapters(context.Registry);
        return adapters.Count > 0 && adapters.All(key =>
            context.Registry.GetValue(RegistryRoot.LocalMachine, key, "PnPCapabilities") == NoPowerManagement &&
            ExistingKeywords(context.Registry, key).All(k => context.Registry.GetValue(RegistryRoot.LocalMachine, key, k) == Off));
    }

    public string? Apply(TweakContext context)
    {
        var adapters = PhysicalAdapters(context.Registry);
        if (adapters.Count == 0)
        {
            throw new InvalidOperationException("Nenhuma placa de rede física foi encontrada.");
        }

        var options = 0;
        foreach (var key in adapters)
        {
            context.SetRegistry(RegistryRoot.LocalMachine, key, "PnPCapabilities", NoPowerManagement);
            foreach (var keyword in ExistingKeywords(context.Registry, key))
            {
                context.SetRegistry(RegistryRoot.LocalMachine, key, keyword, Off);
                options++;
            }
        }

        return $"{adapters.Count} placa(s), {options} opção(ões) de economia desligada(s)";
    }

    /// <summary>Adaptadores Ethernet (IfType 6) e Wi-Fi (IfType 71); ignora adaptadores virtuais (VPN, Hyper-V...).</summary>
    public static IReadOnlyList<string> PhysicalAdapters(IRegistry registry) =>
        registry.GetSubKeyNames(RegistryRoot.LocalMachine, ClassKey)
            .Where(name => FourDigits().IsMatch(name))
            .Select(name => $@"{ClassKey}\{name}")
            .Where(key =>
                registry.GetValue(RegistryRoot.LocalMachine, key, "*IfType")?.Data is "6" or "71" &&
                registry.GetValue(RegistryRoot.LocalMachine, key, "NetCfgInstanceId") is not null &&
                !VirtualAdapter().IsMatch(registry.GetValue(RegistryRoot.LocalMachine, key, "DriverDesc")?.Data ?? ""))
            .ToList();

    private static IEnumerable<string> ExistingKeywords(IRegistry registry, string key) =>
        PowerSavingKeywords.Where(k => registry.GetValue(RegistryRoot.LocalMachine, key, k)?.Type == RegistryValueType.String);

    // "Properties" e outras subchaves protegidas não são adaptadores.
    [GeneratedRegex(@"^\d{4}$")]
    private static partial Regex FourDigits();

    [GeneratedRegex("virtual|hyper-v|vpn|tap-|tunnel|loopback|miniport|bluetooth|vmware|virtualbox", RegexOptions.IgnoreCase)]
    private static partial Regex VirtualAdapter();
}
