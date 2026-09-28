using System.Collections.ObjectModel;
using System.Windows;
using PcOptimizer.Core.Backup;
using PcOptimizer.Core.Bios;
using PcOptimizer.Core.Optimization;
using PcOptimizer.Core.Platform;
using PcOptimizer.Core.Platform.Windows;
using PcOptimizer.Core.Tweaks;

namespace PcOptimizer.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly WindowsRegistry _registry = new();
    private readonly ProcessCommandRunner _commands = new();
    private readonly JsonBackupStore _backups = new(JsonBackupStore.DefaultDirectory);
    private readonly Optimizer _optimizer;

    private bool _isBusy;
    private string _status = "Pronto.";
    private string _summary = "";
    private string _hardwareSummary = "Lendo informações do hardware...";

    public MainViewModel()
    {
        _optimizer = new Optimizer(_registry, _commands, _backups, new WindowsRestorePointService(_commands));
        Tweaks = new ObservableCollection<TweakItemViewModel>(TweakCatalog.All().Select(t => new TweakItemViewModel(t)));

        OptimizeCommand = new RelayCommand(Optimize, () => !IsBusy && Tweaks.Any(t => t.IsSelected));
        UndoLastCommand = new RelayCommand(() => Revert(Backups.FirstOrDefault(b => b.CanRevert)), () => !IsBusy && Backups.Any(b => b.CanRevert));
        RevertCommand = new RelayCommand(p => Revert(p as BackupItemViewModel), p => !IsBusy && p is BackupItemViewModel { CanRevert: true });
        SelectRecommendedCommand = new RelayCommand(() =>
        {
            foreach (var t in Tweaks)
            {
                t.IsSelected = t.Tweak.Recommended;
            }
        });
        RebootToBiosCommand = new RelayCommand(RebootToBios, () => !IsBusy);
        RestartCommand = new RelayCommand(() => _commands.Run("shutdown", "/r /t 5"), () => !IsBusy);
    }

    public ObservableCollection<TweakItemViewModel> Tweaks { get; }

    public ObservableCollection<string> Log { get; } = [];

    public ObservableCollection<BiosItemViewModel> BiosItems { get; } = [];

    public ObservableCollection<BackupItemViewModel> Backups { get; } = [];

    public RelayCommand OptimizeCommand { get; }

    public RelayCommand UndoLastCommand { get; }

    public RelayCommand RevertCommand { get; }

    public RelayCommand SelectRecommendedCommand { get; }

    public RelayCommand RebootToBiosCommand { get; }

    public RelayCommand RestartCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    public string HardwareSummary
    {
        get => _hardwareSummary;
        private set => Set(ref _hardwareSummary, value);
    }

    public string BackupFolder => _backups.Directory;

    public async Task LoadAsync()
    {
        IsBusy = true;
        Status = "Analisando o PC...";
        try
        {
            var applied = await Task.Run(() =>
            {
                var context = new TweakContext(_registry, _commands, new BackupSession());
                return Tweaks.Select(t => SafeIsApplied(t.Tweak, context)).ToList();
            });
            for (var i = 0; i < Tweaks.Count; i++)
            {
                Tweaks[i].IsApplied = applied[i];
            }

            ReloadBackups();

            var report = await Task.Run(() => BiosAdvisor.Analyze(new WmiHardwareProbe(_registry).Probe()));
            ShowBios(report);
            Status = "Pronto.";
        }
        catch (Exception e)
        {
            Status = $"Erro ao analisar: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void Optimize()
    {
        var selected = Tweaks.Where(t => t.IsSelected).Select(t => t.Tweak).ToList();
        IsBusy = true;
        Log.Clear();
        Summary = "";
        var progress = new Progress<string>(message =>
        {
            Status = message;
            Log.Add(message);
        });

        try
        {
            var report = await Task.Run(() => _optimizer.Run(selected, progress));
            foreach (var r in report.Results)
            {
                var icon = r.Status switch
                {
                    TweakStatus.Applied => "✔",
                    TweakStatus.AlreadyApplied => "•",
                    _ => "✖",
                };
                var state = r.Status switch
                {
                    TweakStatus.Applied => "aplicado",
                    TweakStatus.AlreadyApplied => "já estava aplicado",
                    _ => "falhou",
                };
                Log.Add($"{icon} {r.TweakName}: {state}{(r.Detail is null ? "" : $" ({r.Detail})")}");
            }

            Summary = $"{report.AppliedCount} ajustes aplicados" +
                (report.FailedCount > 0 ? $", {report.FailedCount} falharam" : "") +
                (report.RestartRequired ? ". Reinicie o PC para concluir." : ".") +
                (report.BackupId is null ? "" : " Tudo pode ser desfeito na aba Histórico.");
            Status = "Otimização concluída.";

            foreach (var t in Tweaks)
            {
                t.IsApplied |= report.Results.Any(r => r.TweakId == t.Tweak.Id && r.Status != TweakStatus.Failed) && t.Tweak.Reversible;
            }

            ReloadBackups();
        }
        catch (Exception e)
        {
            Status = $"Erro: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void Revert(BackupItemViewModel? item)
    {
        if (item is null ||
            MessageBox.Show($"Desfazer a otimização de {item.Session.CreatedAt:dd/MM/yyyy HH:mm}?", "Desfazer",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        Status = "Desfazendo...";
        string message;
        try
        {
            var result = await Task.Run(() => _optimizer.Revert(item.Session));
            message = result.Errors.Count == 0
                ? $"{result.Restored} alterações desfeitas. Reinicie o PC para garantir que tudo voltou ao normal."
                : $"{result.Restored} alterações desfeitas, {result.Errors.Count} com erro: {string.Join("; ", result.Errors)}";
        }
        catch (Exception e)
        {
            message = $"Erro ao desfazer: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
        Status = message;
    }

    private void RebootToBios()
    {
        if (MessageBox.Show(
                "O PC vai reiniciar em 5 segundos direto na tela da BIOS.\nSalve seus arquivos antes. Continuar?",
                "Reiniciar na BIOS", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var result = FirmwareReboot.Restart(_commands);
        if (!result.Success)
        {
            MessageBox.Show(
                "Este PC não permite reiniciar direto na BIOS (provavelmente está em modo legado).\n" +
                "Reinicie normalmente e aperte a tecla indicada na aba BIOS logo ao ligar.",
                "Reiniciar na BIOS", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ShowBios(BiosReport report)
    {
        var hw = report.Hardware;
        var ram = hw.Memory.Count == 0
            ? "desconhecida"
            : $"{hw.Memory.Sum(m => m.CapacityBytes) >> 30} GB {hw.Memory[0].Generation} em {hw.Memory.Count} pente(s) a {hw.Memory.Min(m => m.ConfiguredSpeedMts)} MT/s";
        HardwareSummary = string.Join(Environment.NewLine,
            $"Placa-mãe: {hw.BoardManufacturer} {hw.BoardProduct}",
            $"BIOS: {hw.BiosVersion}{(hw.BiosReleaseDate is { } d ? $" ({d:dd/MM/yyyy})" : "")} • {(hw.IsUefi switch { true => "UEFI", false => "Legado", _ => "?" })}",
            $"Processador: {hw.CpuName}",
            $"Memória: {ram}",
            $"Vídeo: {string.Join(", ", hw.Gpus)}",
            $"Tecla para entrar na BIOS ({report.Vendor.Name}): {report.Vendor.EnterKeys}");

        BiosItems.Clear();
        foreach (var item in report.Items.OrderBy(i => i.Status switch
                 {
                     AdviceStatus.ActionNeeded => 0,
                     AdviceStatus.CheckManually => 1,
                     AdviceStatus.Info => 2,
                     _ => 3,
                 }))
        {
            BiosItems.Add(new BiosItemViewModel(item));
        }
    }

    private void ReloadBackups()
    {
        Backups.Clear();
        foreach (var session in _backups.LoadAll())
        {
            Backups.Add(new BackupItemViewModel(session));
        }
    }

    private static bool SafeIsApplied(ITweak tweak, TweakContext context)
    {
        try
        {
            return tweak.IsApplied(context);
        }
        catch
        {
            return false;
        }
    }
}
