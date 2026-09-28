using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Slyth.Core.Optimization;
using Slyth.Core.Performance;

namespace Slyth.App.ViewModels;

public sealed class MetricRowViewModel(MetricComparison metric, bool compare)
{
    public string Name => metric.Name;

    public string Unit => metric.Unit;

    public string Value => metric.AfterText;

    public string Before => metric.BeforeText;

    public string Delta => metric.DeltaText;

    public string Hint => metric.Hint;

    public bool HasComparison => compare && metric.Verdict != MetricVerdict.Unknown;

    public bool IsBetter => compare && metric.Verdict == MetricVerdict.Better;

    public bool IsWorse => compare && metric.Verdict == MetricVerdict.Worse;

    public bool IsSame => compare && metric.Verdict == MetricVerdict.Same;

    public double BeforeBar => Bar(metric.Before);

    public double AfterBar => Bar(metric.After);

    private double Bar(double? v)
    {
        var max = Math.Max(metric.Before ?? 0, metric.After ?? 0);
        return v is { } x && max > 0 ? 100 * x / max : 0;
    }
}

/// <summary>
/// Tela "Desempenho": mede o PC, guarda cada medição e compara a primeira (antes) com a última (agora).
/// A primeira medição é feita sozinha na primeira abertura, e a de depois, sozinha, após reiniciar pós-otimização.
/// </summary>
public sealed class PerformanceViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IPerformanceProbe _probe;
    private readonly SnapshotStore _store;
    private readonly Func<double> _score;
    private readonly Action<string, string, string, Action?> _dialog;
    private readonly Action _showPage;

    private bool _isMeasuring;
    private double _progress;
    private string _progressText = "";
    private string _headline = "Ainda não medimos seu PC";
    private string _subline = "";
    private string _dates = "";
    private string _shortSummary = "Não medido";

    public PerformanceViewModel(IPerformanceProbe probe, SnapshotStore store, Func<double> score,
        Action<string, string, string, Action?> dialog, Action showPage)
    {
        _probe = probe;
        _store = store;
        _score = score;
        _dialog = dialog;
        _showPage = showPage;
        MeasureCommand = new RelayCommand(async () => await MeasureAsync("Medição manual"), () => !IsMeasuring);
        ResetBaselineCommand = new RelayCommand(ConfirmReset, () => !IsMeasuring && _store.LoadAll().Count > 1);
    }

    public ObservableCollection<MetricRowViewModel> Metrics { get; } = [];

    public RelayCommand MeasureCommand { get; }

    public RelayCommand ResetBaselineCommand { get; }

    public bool IsMeasuring
    {
        get => _isMeasuring;
        private set
        {
            if (Set(ref _isMeasuring, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public double Progress { get => _progress; private set => Set(ref _progress, value); }

    public string ProgressText { get => _progressText; private set => Set(ref _progressText, value); }

    public string Headline { get => _headline; private set => Set(ref _headline, value); }

    public string Subline { get => _subline; private set => Set(ref _subline, value); }

    public string Dates { get => _dates; private set => Set(ref _dates, value); }

    public string ShortSummary { get => _shortSummary; private set => Set(ref _shortSummary, value); }

    /// <summary>Chamado ao abrir o app: mede a base na primeira vez e o "depois" após reiniciar pós-otimização.</summary>
    public async Task AutoMeasureAsync(DateTime? lastOptimization)
    {
        Refresh();
        var snapshots = _store.LoadAll();
        if (snapshots.Count == 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(4)); // deixa o próprio app terminar de abrir
            await MeasureAsync("Antes da otimização");
            return;
        }

        var lastBoot = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);
        if (lastOptimization is { } optimized && optimized > snapshots[^1].TakenAt && lastBoot > optimized)
        {
            // O Windows só registra o tempo de inicialização alguns minutos depois de ligar.
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            var wait = TimeSpan.FromMinutes(3) - uptime;
            if (wait > TimeSpan.Zero)
            {
                ShortSummary = "Medindo em instantes";
                await Task.Delay(wait);
            }

            await MeasureAsync("Depois da otimização");
            var better = PerformanceComparison.Compare(_store.LoadAll()[0], _store.LoadAll()[^1]).Count(m => m.Verdict == MetricVerdict.Better);
            _dialog("Seu PC foi medido de novo",
                $"Depois da otimização e do reinício, {better} métricas melhoraram em relação à primeira medição.",
                "Ver resultado", _showPage);
        }
    }

    public async Task MeasureAsync(string label)
    {
        if (IsMeasuring)
        {
            return;
        }

        IsMeasuring = true;
        Progress = 0;
        ShortSummary = "Medindo...";
        var progress = new Progress<OptimizationProgress>(p =>
        {
            Progress = p.Fraction;
            ProgressText = p.Message;
        });

        try
        {
            var snapshot = await PerformanceBenchmark.RunAsync(_probe, label, _score(), progress);
            _store.Save(snapshot);
        }
        catch (Exception e)
        {
            ProgressText = $"Não foi possível medir: {e.Message}";
        }
        finally
        {
            IsMeasuring = false;
            Refresh();
        }
    }

    public void Refresh()
    {
        var all = _store.LoadAll();
        Metrics.Clear();
        if (all.Count == 0)
        {
            Headline = "Ainda não medimos seu PC";
            Subline = "Meça agora para ter uma base de comparação antes de otimizar.";
            Dates = "";
            ShortSummary = "Não medido";
            return;
        }

        var first = all[0];
        var last = all[^1];
        var compare = all.Count > 1;
        var results = PerformanceComparison.Compare(compare ? first : null, last);
        foreach (var r in results)
        {
            Metrics.Add(new MetricRowViewModel(r, compare));
        }

        if (!compare)
        {
            Headline = "Base medida. Agora otimize.";
            Subline = "Depois de otimizar e reiniciar, o Slyth mede de novo sozinho e mostra aqui o ganho real, métrica por métrica.";
            Dates = $"Medido em {Date(last.TakenAt)}";
            ShortSummary = "Base medida";
            return;
        }

        var comparable = results.Count(r => r.Verdict != MetricVerdict.Unknown);
        var better = results.Where(r => r.Verdict == MetricVerdict.Better).ToList();
        var best = better.OrderByDescending(r => r.Gain).FirstOrDefault();
        Headline = $"{better.Count} de {comparable} métricas melhoraram";
        Subline = best is null
            ? "Nenhuma diferença acima da margem normal de variação ainda. Reinicie o PC e meça de novo."
            : $"Maior ganho: {best.Name.ToLower(PtBr)} ({best.DeltaText}).";
        Dates = $"Antes: {Date(first.TakenAt)}   ·   Agora: {Date(last.TakenAt)}";
        ShortSummary = $"{better.Count} de {comparable} melhores";
    }

    private void ConfirmReset() => _dialog("Usar a última medição como base?",
        "As medições antigas são apagadas e a comparação passa a partir da medição mais recente.",
        "Definir nova base", () =>
        {
            _store.ResetBaseline();
            Refresh();
        });

    private static string Date(DateTime d) => d.ToString("dd/MM 'às' HH:mm", PtBr);
}
