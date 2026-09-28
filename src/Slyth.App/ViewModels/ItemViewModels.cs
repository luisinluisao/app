using Slyth.Core.Backup;
using Slyth.Core.Bios;
using Slyth.Core.Optimization;
using Slyth.Core.Tweaks;

namespace Slyth.App.ViewModels;

public sealed class TweakItemViewModel(ITweak tweak, Action onSelectionChanged) : ObservableObject
{
    private bool _isSelected;
    private bool _isApplied;

    public ITweak Tweak { get; } = tweak;

    public string Name => Tweak.Name;

    public string Description => Tweak.Description;

    public string Category => Tweak.Category.DisplayName();

    public string LevelName => Tweak.Level.DisplayName();

    public bool RequiresRestart => Tweak.RequiresRestart;

    public bool NotReversible => !Tweak.Reversible;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value))
            {
                onSelectionChanged();
            }
        }
    }

    public bool IsApplied
    {
        get => _isApplied;
        set => Set(ref _isApplied, value);
    }
}

public sealed record PresetOption(Preset Preset, string Name, string Description);

public sealed record SpecRow(string Label, string Value);

public sealed record StepRow(int Number, string Text);

public sealed class BiosItemViewModel(BiosRecommendation item)
{
    public string Title => item.Title;

    public string Explanation => item.Explanation;

    public IReadOnlyList<StepRow> Steps { get; } = item.Steps.Select((s, i) => new StepRow(i + 1, s)).ToList();

    public bool HasSteps => Steps.Count > 0;

    public bool NeedsAction => item.Status == AdviceStatus.ActionNeeded;

    public bool IsOk => item.Status == AdviceStatus.Ok;

    public string StatusText => item.Status switch
    {
        AdviceStatus.Ok => "OK",
        AdviceStatus.ActionNeeded => "AÇÃO RECOMENDADA",
        AdviceStatus.CheckManually => "VERIFICAR",
        _ => "INFO",
    };
}

public sealed class BackupItemViewModel(BackupSession session)
{
    public BackupSession Session { get; } = session;

    public string Date => Session.CreatedAt.ToString("dd 'de' MMMM, HH:mm", new System.Globalization.CultureInfo("pt-BR"));

    public string Title => $"{Session.AppliedTweaks.Count} ajustes aplicados";

    public string Detail => Session.RevertedAt is { } at ? $"Desfeito em {at:dd/MM/yyyy 'às' HH:mm}" : $"{Session.Changes.Count} alterações salvas no backup";

    public bool CanRevert => Session.RevertedAt is null;
}

public sealed class ResultRow(TweakResult result)
{
    public string Name => result.TweakName;

    public string Glyph => result.Status switch
    {
        TweakStatus.Applied => "",
        TweakStatus.AlreadyApplied => "",
        _ => "",
    };

    public string Detail => result.Status switch
    {
        TweakStatus.Applied => result.Detail ?? "aplicado",
        TweakStatus.AlreadyApplied => "já estava aplicado",
        _ => result.Detail ?? "falhou",
    };

    public double Opacity => result.Status == TweakStatus.AlreadyApplied ? 0.45 : 1;
}
