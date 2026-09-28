namespace PcOptimizer.Core.Tweaks;

public enum TweakCategory
{
    Performance,
    Gaming,
    Privacy,
    Cleanup,
    Appearance,
}

public interface ITweak
{
    string Id { get; }

    string Name { get; }

    string Description { get; }

    TweakCategory Category { get; }

    /// <summary>Incluído no botão de um clique.</summary>
    bool Recommended { get; }

    bool RequiresRestart { get; }

    /// <summary>False para ações que não podem ser desfeitas (ex.: apagar temporários).</summary>
    bool Reversible { get; }

    bool IsApplied(TweakContext context);

    /// <summary>Aplica o ajuste e retorna um detalhe opcional para o relatório.</summary>
    string? Apply(TweakContext context);
}

public static class TweakCategoryExtensions
{
    public static string DisplayName(this TweakCategory category) => category switch
    {
        TweakCategory.Performance => "Desempenho",
        TweakCategory.Gaming => "Jogos",
        TweakCategory.Privacy => "Privacidade e segundo plano",
        TweakCategory.Cleanup => "Limpeza",
        TweakCategory.Appearance => "Aparência e mouse",
        _ => category.ToString(),
    };
}
