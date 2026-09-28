using System.Globalization;

namespace Slyth.Core.Performance;

public enum MetricVerdict
{
    Better,
    Worse,
    Same,
    Unknown,
}

public sealed record MetricComparison(
    string Key,
    string Name,
    string Unit,
    double? Before,
    double? After,
    MetricVerdict Verdict,
    string DeltaText,
    string Hint)
{
    public string AfterText => Format(After);

    public string BeforeText => Format(Before);

    /// <summary>Melhora relativa (0.25 = 25% melhor); negativo quando piorou.</summary>
    public double Gain { get; init; }

    /// <summary>Contagens sem casas decimais; medidas com uma casa (ex.: 7,4 GB).</summary>
    private string Format(double? v) => v is not { } value
        ? "—"
        : value.ToString(Unit.Length == 0 || value >= 100 ? "0" : "0.#", CultureInfo.GetCultureInfo("pt-BR"));
}

/// <summary>Compara duas medições com margens de ruído, para não chamar de "ganho" uma variação normal.</summary>
public static class PerformanceComparison
{
    private sealed record Metric(
        string Key, string Name, string Unit, bool LowerIsBetter, double NoiseRatio, double NoiseAbsolute, bool ShowPercent,
        Func<PerformanceSnapshot, double?> Read, string Hint);

    private static readonly Metric[] Metrics =
    [
        new("boot", "Inicialização do Windows", "s", true, 0.05, 1, true, s => s.BootSeconds,
            "Tempo do Windows até a área de trabalho pronta, medido pelo próprio Windows."),
        new("ram", "Memória em uso", "GB", true, 0.04, 0.1, false, s => s.RamUsedGb,
            "RAM ocupada com o PC parado. Menos uso = mais sobra para jogos."),
        new("cpu", "CPU em repouso", "%", true, 0, 1, false, s => s.IdleCpuPercent,
            "Quanto o processador trabalha sem você fazer nada."),
        new("processes", "Processos", "", true, 0.03, 2, false, s => s.Processes,
            "Programas e partes do Windows rodando agora."),
        new("services", "Serviços ativos", "", true, 0.02, 1, false, s => s.RunningServices,
            "Serviços do Windows e de outros programas em execução."),
        new("startup", "Apps na inicialização", "", true, 0, 0, false, s => s.StartupApps,
            "Programas que abrem sozinhos com o Windows."),
        new("ping", "Ping", "ms", true, 0.1, 2, true, s => s.PingMs,
            "Latência até a internet (1.1.1.1)."),
        new("jitter", "Variação do ping", "ms", true, 0.15, 1, true, s => s.JitterMs,
            "Quanto o ping oscila. Variação alta causa travadas online."),
        new("dns", "Resposta do DNS", "ms", true, 0.1, 3, true, s => s.DnsMs,
            "Tempo para descobrir o endereço de sites e servidores."),
        new("disk", "Espaço livre", "GB", false, 0.005, 0.2, false, s => s.DiskFreeGb,
            "Espaço livre no disco do Windows."),
        new("score", "Pontuação Slyth", "", false, 0, 1, false, s => s.Score,
            "Quantos ajustes recomendados estão ativos (0 a 100)."),
    ];

    public static IReadOnlyList<MetricComparison> Compare(PerformanceSnapshot? before, PerformanceSnapshot after) =>
        Metrics.Select(m => Compare(m, before is null ? null : m.Read(before), m.Read(after))).ToList();

    private static MetricComparison Compare(Metric m, double? before, double? after)
    {
        if (before is not { } b || after is not { } a)
        {
            return new MetricComparison(m.Key, m.Name, m.Unit, before, after, MetricVerdict.Unknown, "", m.Hint);
        }

        var diff = a - b;
        var noise = Math.Max(Math.Abs(b) * m.NoiseRatio, m.NoiseAbsolute);
        var verdict = Math.Abs(diff) <= noise
            ? MetricVerdict.Same
            : (diff < 0) == m.LowerIsBetter ? MetricVerdict.Better : MetricVerdict.Worse;

        var gain = b == 0 ? 0 : (m.LowerIsBetter ? -diff : diff) / Math.Abs(b);
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var sign = diff > 0 ? "+" : "−";
        var delta = verdict == MetricVerdict.Same
            ? "sem mudança"
            : m.ShowPercent && b != 0
                ? $"{sign}{Math.Abs(diff / b):0%}".Replace(" ", "")
                : $"{sign}{Math.Abs(diff).ToString(Math.Abs(diff) % 1 < 0.05 ? "0" : "0.#", culture)}{(m.Unit.Length > 0 && m.Unit != "%" ? " " + m.Unit : m.Unit)}";

        return new MetricComparison(m.Key, m.Name, m.Unit, before, after, verdict, delta, m.Hint) { Gain = gain };
    }
}
