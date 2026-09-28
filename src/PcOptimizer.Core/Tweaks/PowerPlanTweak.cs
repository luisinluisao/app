using System.Text.RegularExpressions;

namespace PcOptimizer.Core.Tweaks;

/// <summary>Ativa o plano de energia "Alto desempenho", criando uma cópia se ele estiver oculto.</summary>
public sealed partial class PowerPlanTweak : ITweak
{
    public const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string UltimatePerformance = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    public const string OwnSchemeName = "PC Optimizer - Alto desempenho";

    public string Id => "power-plan";

    public string Name => "Plano de energia de alto desempenho";

    public string Description => "Mantém o processador pronto para trabalhar em vez de economizar energia. " +
        "Em notebooks, gasta mais bateria: ideal usando na tomada.";

    public TweakCategory Category => TweakCategory.Performance;

    public bool Recommended => true;

    public bool RequiresRestart => false;

    public bool Reversible => true;

    public bool IsApplied(TweakContext context)
    {
        var active = GetActive(context);
        if (active is null)
        {
            return false;
        }

        return active.Value.Guid is HighPerformance or UltimatePerformance || active.Value.Name == OwnSchemeName;
    }

    public string? Apply(TweakContext context)
    {
        var previous = GetActive(context) ?? throw new InvalidOperationException("Não foi possível ler o plano de energia atual.");
        var schemes = ListSchemes(context);

        string target;
        if (schemes.Any(s => s.Guid == HighPerformance))
        {
            target = HighPerformance;
        }
        else if (schemes.FirstOrDefault(s => s.Name == OwnSchemeName) is { Guid: not null } own)
        {
            target = own.Guid;
        }
        else
        {
            // Em muitos notebooks o plano existe mas fica oculto: duplicar o torna utilizável.
            var dup = context.Commands.Run("powercfg", $"/duplicatescheme {HighPerformance}");
            target = FindGuid(dup.Output) ?? throw new InvalidOperationException("Este PC não permite o plano de alto desempenho.");
            context.Commands.Run("powercfg", $"/changename {target} \"{OwnSchemeName}\"");
        }

        context.RecordPowerScheme(previous.Guid);
        var result = context.Commands.Run("powercfg", $"/setactive {target}");
        if (!result.Success)
        {
            throw new InvalidOperationException($"powercfg falhou: {result.Error.Trim()}");
        }

        return $"Plano anterior: {previous.Name}";
    }

    private static (string Guid, string Name)? GetActive(TweakContext context)
    {
        var output = context.Commands.Run("powercfg", "/getactivescheme").Output;
        var match = SchemeLine().Match(output);
        return match.Success ? (match.Groups["guid"].Value.ToLowerInvariant(), match.Groups["name"].Value) : null;
    }

    private static List<(string Guid, string Name)> ListSchemes(TweakContext context) =>
        SchemeLine().Matches(context.Commands.Run("powercfg", "/list").Output)
            .Select(m => (m.Groups["guid"].Value.ToLowerInvariant(), m.Groups["name"].Value))
            .ToList();

    private static string? FindGuid(string text)
    {
        var match = SchemeLine().Match(text);
        return match.Success ? match.Groups["guid"].Value.ToLowerInvariant() : null;
    }

    // A saída do powercfg é traduzida ("Power Scheme GUID" / "GUID do Esquema de Energia"), então só confiamos no GUID e no nome entre parênteses.
    [GeneratedRegex(@"(?<guid>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s*(\((?<name>[^)]*)\))?")]
    private static partial Regex SchemeLine();
}
