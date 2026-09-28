namespace Slyth.Core.Tweaks;

public enum TweakCategory
{
    Performance,
    Gaming,
    Network,
    Privacy,
    Services,
    Cleanup,
    Appearance,
}

/// <summary>
/// Em quais modos o ajuste entra. Cada modo inclui os anteriores:
/// Seguro ⊂ Gamer ⊂ Extremo. Ajustes Manual nunca são marcados automaticamente.
/// </summary>
public enum TweakLevel
{
    Safe,
    Gamer,
    Extreme,
    Manual,
}

public interface ITweak
{
    string Id { get; }

    string Name { get; }

    string Description { get; }

    TweakCategory Category { get; }

    TweakLevel Level { get; }

    bool RequiresRestart { get; }

    /// <summary>False para ações que não podem ser desfeitas (ex.: apagar temporários).</summary>
    bool Reversible { get; }

    bool IsApplied(TweakContext context);

    /// <summary>Aplica o ajuste e retorna um detalhe opcional para o relatório.</summary>
    string? Apply(TweakContext context);
}

public static class TweakEnumExtensions
{
    public static string DisplayName(this TweakCategory category) => category switch
    {
        TweakCategory.Performance => "Desempenho",
        TweakCategory.Gaming => "Jogos",
        TweakCategory.Network => "Rede",
        TweakCategory.Privacy => "Privacidade",
        TweakCategory.Services => "Serviços",
        TweakCategory.Cleanup => "Limpeza",
        TweakCategory.Appearance => "Visual e entrada",
        _ => category.ToString(),
    };

    public static string DisplayName(this TweakLevel level) => level switch
    {
        TweakLevel.Safe => "Seguro",
        TweakLevel.Gamer => "Gamer",
        TweakLevel.Extreme => "Extremo",
        _ => "Manual",
    };
}
