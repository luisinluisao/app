using PcOptimizer.Core.Platform;

namespace PcOptimizer.Core.Tweaks;

public sealed record RegistrySetting(RegistryRoot Root, string Key, string Name, RegistryValue Value);

/// <summary>Ajuste definido só por valores de registro (e, opcionalmente, comandos depois de aplicar).</summary>
public sealed class RegistryTweak : ITweak
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required TweakCategory Category { get; init; }

    public bool Recommended { get; init; }

    public bool RequiresRestart { get; init; }

    public bool Reversible => true;

    public required IReadOnlyList<RegistrySetting> Settings { get; init; }

    /// <summary>Comandos executados após gravar o registro (ex.: parar um serviço). Falhas não desfazem o ajuste.</summary>
    public IReadOnlyList<(string File, string Args)> AfterApply { get; init; } = [];

    public bool IsApplied(TweakContext context) =>
        Settings.All(s => context.Registry.GetValue(s.Root, s.Key, s.Name) == s.Value);

    public string? Apply(TweakContext context)
    {
        foreach (var s in Settings)
        {
            context.SetRegistry(s.Root, s.Key, s.Name, s.Value);
        }

        foreach (var (file, args) in AfterApply)
        {
            context.Commands.Run(file, args);
        }

        return null;
    }
}
