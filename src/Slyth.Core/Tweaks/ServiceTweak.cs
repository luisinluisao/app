using Slyth.Core.Platform;

namespace Slyth.Core.Tweaks;

/// <summary>
/// Desativa serviços do Windows (Start = 4) e os para imediatamente.
/// Serviços que não existem neste PC são ignorados, sem criar chaves no registro.
/// </summary>
public sealed class ServiceTweak : ITweak
{
    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";
    private static readonly RegistryValue Disabled = RegistryValue.Dword(4);

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public TweakCategory Category { get; init; } = TweakCategory.Services;

    public TweakLevel Level { get; init; } = TweakLevel.Manual;

    public bool RequiresRestart => false;

    public bool Reversible => true;

    public required IReadOnlyList<string> Services { get; init; }

    /// <summary>Valores de registro extras aplicados junto (ex.: políticas).</summary>
    public IReadOnlyList<RegistrySetting> AlsoSet { get; init; } = [];

    public bool IsApplied(TweakContext context) =>
        Services.All(s => StartValue(context, s) is null || StartValue(context, s) == Disabled) &&
        AlsoSet.All(s => context.Registry.GetValue(s.Root, s.Key, s.Name) == s.Value);

    public string? Apply(TweakContext context)
    {
        var disabled = 0;
        foreach (var service in Services)
        {
            if (StartValue(context, service) is null)
            {
                continue;
            }

            context.SetRegistry(RegistryRoot.LocalMachine, $@"{ServicesKey}\{service}", "Start", Disabled);
            context.Commands.Run("sc", $"stop {service}");
            disabled++;
        }

        foreach (var s in AlsoSet)
        {
            context.SetRegistry(s.Root, s.Key, s.Name, s.Value);
        }

        return disabled == 0 ? "não instalado neste PC" : $"{disabled} serviço(s) desativado(s)";
    }

    private static RegistryValue? StartValue(TweakContext context, string service) =>
        context.Registry.GetValue(RegistryRoot.LocalMachine, $@"{ServicesKey}\{service}", "Start");
}
