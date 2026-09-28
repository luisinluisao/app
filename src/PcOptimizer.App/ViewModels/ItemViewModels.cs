using System.Windows.Media;
using PcOptimizer.Core.Backup;
using PcOptimizer.Core.Bios;
using PcOptimizer.Core.Tweaks;

namespace PcOptimizer.App.ViewModels;

public sealed class TweakItemViewModel(ITweak tweak) : ObservableObject
{
    private bool _isSelected = tweak.Recommended;
    private bool _isApplied;

    public ITweak Tweak { get; } = tweak;

    public string Name => Tweak.Name;

    public string Description => Tweak.Description;

    public string Category => Tweak.Category.DisplayName();

    public bool RequiresRestart => Tweak.RequiresRestart;

    public bool NotReversible => !Tweak.Reversible;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public bool IsApplied
    {
        get => _isApplied;
        set => Set(ref _isApplied, value);
    }
}

public sealed class BiosItemViewModel(BiosRecommendation item)
{
    public string Title => item.Title;

    public string Explanation => item.Explanation;

    public IReadOnlyList<string> Steps { get; } = item.Steps.Select((s, i) => $"{i + 1}. {s}").ToList();

    public bool HasSteps => Steps.Count > 0;

    public string StatusText => item.Status switch
    {
        AdviceStatus.Ok => "OK",
        AdviceStatus.ActionNeeded => "AÇÃO RECOMENDADA",
        AdviceStatus.CheckManually => "VERIFICAR",
        _ => "INFORMAÇÃO",
    };

    public Brush StatusBrush => item.Status switch
    {
        AdviceStatus.Ok => Palette.Green,
        AdviceStatus.ActionNeeded => Palette.Amber,
        AdviceStatus.CheckManually => Palette.Blue,
        _ => Palette.Gray,
    };
}

public sealed class BackupItemViewModel(BackupSession session)
{
    public BackupSession Session { get; } = session;

    public string Title => $"{Session.CreatedAt:dd/MM/yyyy HH:mm} — {Session.AppliedTweaks.Count} ajustes";

    public string Detail => Session.RevertedAt is { } at
        ? $"Desfeito em {at:dd/MM/yyyy HH:mm}"
        : string.Join(", ", Session.AppliedTweaks);

    public bool CanRevert => Session.RevertedAt is null;
}

internal static class Palette
{
    public static readonly Brush Green = Freeze(0x22, 0xC5, 0x5E);
    public static readonly Brush Amber = Freeze(0xF5, 0x9E, 0x0B);
    public static readonly Brush Blue = Freeze(0x3B, 0x82, 0xF6);
    public static readonly Brush Gray = Freeze(0x8B, 0x92, 0xA0);

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
