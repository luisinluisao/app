using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Security.Principal;
using System.Windows.Data;
using Slyth.App.Services;
using Slyth.Core;
using Slyth.Core.Backup;
using Slyth.Core.Bios;
using Slyth.Core.Optimization;
using Slyth.Core.Performance;
using Slyth.Core.Platform.Windows;
using Slyth.Core.Startup;
using Slyth.Core.Tweaks;

namespace Slyth.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const string AllCategories = "Todas";

    private readonly WindowsRegistry _registry = new();
    private readonly ProcessCommandRunner _commands = new();
    private readonly JsonBackupStore _backups = new(JsonBackupStore.DefaultDirectory);
    private readonly Optimizer _optimizer;

    private string _currentPage = "Dashboard";
    private PresetOption _selectedPreset;
    private string _selectedCategory = AllCategories;
    private string _searchText = "";
    private bool _isBusy;
    private double _progress;
    private string _progressText = "";
    private double _score;
    private int _appliedCount;
    private int _selectedCount;
    private int _biosActionCount;
    private string _lastRun = "Nunca";
    private string _machineSummary = "";
    private string _vendorKeys = "";
    private bool _isResultOpen;
    private string _resultApplied = "", _resultFreed = "", _resultFailed = "";
    private bool _resultRestart;
    private bool _isDialogOpen;
    private string _dialogTitle = "", _dialogMessage = "", _dialogConfirm = "";
    private Action? _dialogAction;

    public MainViewModel()
    {
        _optimizer = new Optimizer(_registry, _commands, _backups, new WindowsRestorePointService(_commands));
        Network = new NetworkViewModel(_optimizer, async () =>
        {
            ReloadBackups();
            await RefreshAppliedAsync();
        }, ShowDialog);
        var startupManager = StartupManager.ForCurrentUser(_registry);
        Startup = new StartupViewModel(startupManager);
        Performance = new PerformanceViewModel(
            new WindowsPerformanceProbe(_commands, startupManager),
            new SnapshotStore(SnapshotStore.DefaultDirectory),
            () => Score,
            ShowDialog,
            () => CurrentPage = "Performance");

        Tweaks = new ObservableCollection<TweakItemViewModel>(TweakCatalog.All().Select(t => new TweakItemViewModel(t, UpdateCounts)));
        TweaksView = CollectionViewSource.GetDefaultView(Tweaks);
        TweaksView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TweakItemViewModel.Category)));
        TweaksView.Filter = o => o is TweakItemViewModel t &&
            (SelectedCategory == AllCategories || t.Category == SelectedCategory) &&
            (SearchText.Length == 0 ||
             t.Name.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
             t.Description.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));

        Categories = [AllCategories, .. Enum.GetValues<TweakCategory>().Select(c => c.DisplayName())];
        PresetOptions = Enum.GetValues<Preset>().Select(p => new PresetOption(p, p.DisplayName(), p.Description())).ToList();
        _selectedPreset = PresetOptions[1];
        ApplyPreset(_selectedPreset.Preset);

        IsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        NavigateCommand = new RelayCommand(p => CurrentPage = p as string ?? "Dashboard");
        OptimizeCommand = new RelayCommand(Optimize, () => !IsBusy && SelectedCount > 0 && !Performance.IsMeasuring);
        SelectAllCommand = new RelayCommand(() => SetSelection(t => TweaksView.Filter(t) || t.IsSelected));
        SelectNoneCommand = new RelayCommand(() => SetSelection(_ => false));
        UndoLastCommand = new RelayCommand(() => ConfirmRevert(Backups.FirstOrDefault(b => b.CanRevert)), () => !IsBusy && Backups.Any(b => b.CanRevert));
        RevertCommand = new RelayCommand(p => ConfirmRevert(p as BackupItemViewModel), p => !IsBusy && p is BackupItemViewModel { CanRevert: true });
        RebootToBiosCommand = new RelayCommand(ConfirmRebootToBios, () => !IsBusy);
        RestartCommand = new RelayCommand(() => _commands.Run("shutdown", "/r /t 3"));
        CloseResultCommand = new RelayCommand(() => IsResultOpen = false);
        ConfirmDialogCommand = new RelayCommand(() =>
        {
            IsDialogOpen = false;
            _dialogAction?.Invoke();
        });
        CancelDialogCommand = new RelayCommand(() => IsDialogOpen = false);
    }

    public SystemMonitor Monitor { get; } = new();

    public NetworkViewModel Network { get; }

    public StartupViewModel Startup { get; }

    public PerformanceViewModel Performance { get; }

    public ObservableCollection<TweakItemViewModel> Tweaks { get; }

    public ICollectionView TweaksView { get; }

    public IReadOnlyList<string> Categories { get; }

    public IReadOnlyList<PresetOption> PresetOptions { get; }

    public ObservableCollection<ResultRow> Results { get; } = [];

    public ObservableCollection<SpecRow> Hardware { get; } = [];

    public ObservableCollection<BiosItemViewModel> BiosItems { get; } = [];

    public ObservableCollection<BackupItemViewModel> Backups { get; } = [];

    public RelayCommand NavigateCommand { get; }

    public RelayCommand OptimizeCommand { get; }

    public RelayCommand SelectAllCommand { get; }

    public RelayCommand SelectNoneCommand { get; }

    public RelayCommand UndoLastCommand { get; }

    public RelayCommand RevertCommand { get; }

    public RelayCommand RebootToBiosCommand { get; }

    public RelayCommand RestartCommand { get; }

    public RelayCommand CloseResultCommand { get; }

    public RelayCommand ConfirmDialogCommand { get; }

    public RelayCommand CancelDialogCommand { get; }

    public bool IsAdmin { get; }

    public int TotalCount => Tweaks.Count;

    public string BackupFolder => _backups.Directory;

    public string CurrentPage
    {
        get => _currentPage;
        set => Set(ref _currentPage, value);
    }

    public PresetOption SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (value is not null && Set(ref _selectedPreset, value))
            {
                ApplyPreset(value.Preset);
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (Set(ref _selectedCategory, value ?? AllCategories))
            {
                TweaksView.Refresh();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value ?? ""))
            {
                TweaksView.Refresh();
            }
        }
    }

    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    public double Progress { get => _progress; private set => Set(ref _progress, value); }

    public string ProgressText { get => _progressText; private set => Set(ref _progressText, value); }

    public double Score
    {
        get => _score;
        private set
        {
            if (Set(ref _score, value))
            {
                OnPropertyChanged(nameof(ScoreLabel));
            }
        }
    }

    public string ScoreLabel => Score switch
    {
        >= 85 => "No limite",
        >= 60 => "Muito bom",
        >= 35 => "Pode melhorar",
        _ => "Precisa de atenção",
    };

    public int AppliedCount { get => _appliedCount; private set => Set(ref _appliedCount, value); }

    public int SelectedCount { get => _selectedCount; private set => Set(ref _selectedCount, value); }

    public int BiosActionCount { get => _biosActionCount; private set => Set(ref _biosActionCount, value); }

    public string LastRun { get => _lastRun; private set => Set(ref _lastRun, value); }

    public string MachineSummary { get => _machineSummary; private set => Set(ref _machineSummary, value); }

    public string VendorKeys { get => _vendorKeys; private set => Set(ref _vendorKeys, value); }

    public bool IsResultOpen { get => _isResultOpen; private set { Set(ref _isResultOpen, value); OnPropertyChanged(nameof(IsOverlayOpen)); } }

    public string ResultApplied { get => _resultApplied; private set => Set(ref _resultApplied, value); }

    public string ResultFreed { get => _resultFreed; private set => Set(ref _resultFreed, value); }

    public string ResultFailed { get => _resultFailed; private set => Set(ref _resultFailed, value); }

    public bool ResultRestart { get => _resultRestart; private set => Set(ref _resultRestart, value); }

    public bool IsDialogOpen { get => _isDialogOpen; private set { Set(ref _isDialogOpen, value); OnPropertyChanged(nameof(IsOverlayOpen)); } }

    public bool IsOverlayOpen => IsResultOpen || IsDialogOpen;

    public string DialogTitle { get => _dialogTitle; private set => Set(ref _dialogTitle, value); }

    public string DialogMessage { get => _dialogMessage; private set => Set(ref _dialogMessage, value); }

    public string DialogConfirm { get => _dialogConfirm; private set => Set(ref _dialogConfirm, value); }

    public async Task LoadAsync()
    {
        Monitor.Start();
        MachineSummary = $"{Environment.MachineName} · {WindowsName()}";
        await RefreshAppliedAsync();
        ReloadBackups();
        await Startup.LoadAsync();

        try
        {
            Network.LoadAdapterInfo();
        }
        catch (Exception)
        {
            // Sem rede: a tela de Rede mostra o estado vazio.
        }

        try
        {
            ShowBios(await Task.Run(() => BiosAdvisor.Analyze(new WmiHardwareProbe(_registry).Probe())));
        }
        catch (Exception e)
        {
            Hardware.Add(new SpecRow("Erro", e.Message));
        }

        await Performance.AutoMeasureAsync(Backups.FirstOrDefault()?.Session.CreatedAt);
    }

    private async void Optimize()
    {
        var selected = Tweaks.Where(t => t.IsSelected).Select(t => t.Tweak).ToList();
        IsBusy = true;
        Progress = 0;
        ProgressText = "Preparando...";
        var progress = new Progress<OptimizationProgress>(p =>
        {
            Progress = p.Fraction;
            ProgressText = p.Message;
        });

        try
        {
            var report = await Task.Run(() => _optimizer.Run(selected, progress));

            Results.Clear();
            foreach (var r in report.Results.OrderBy(r => r.Status == TweakStatus.AlreadyApplied))
            {
                Results.Add(new ResultRow(r));
            }

            ResultApplied = report.AppliedCount.ToString();
            ResultFreed = report.FreedBytes > 0 ? Bytes.Format(report.FreedBytes) : "0 MB";
            ResultFailed = report.FailedCount.ToString();
            ResultRestart = report.RestartRequired;
            IsResultOpen = true;

            ReloadBackups();
            await RefreshAppliedAsync();
        }
        catch (Exception e)
        {
            ShowDialog("Algo deu errado", e.Message, "OK", null);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ConfirmRevert(BackupItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        ShowDialog("Desfazer otimização?",
            $"Todas as configurações alteradas em {item.Date} voltam a ser como eram antes.",
            "Desfazer", async () =>
            {
                IsBusy = true;
                try
                {
                    var result = await Task.Run(() => _optimizer.Revert(item.Session));
                    ReloadBackups();
                    await RefreshAppliedAsync();
                    ShowDialog(result.Errors.Count == 0 ? "Pronto" : "Desfeito com avisos",
                        result.Errors.Count == 0
                            ? $"{result.Restored} alterações desfeitas. Reinicie o PC para garantir que tudo voltou ao normal."
                            : $"{result.Restored} alterações desfeitas. Com erro: {string.Join("; ", result.Errors)}",
                        "OK", null);
                }
                finally
                {
                    IsBusy = false;
                }
            });
    }

    private void ConfirmRebootToBios() => ShowDialog("Reiniciar na BIOS?",
        "O PC vai reiniciar em 5 segundos direto na tela da BIOS. Salve seus arquivos antes.",
        "Reiniciar agora", () =>
        {
            if (!FirmwareReboot.Restart(_commands).Success)
            {
                ShowDialog("Não foi possível",
                    $"Este PC não permite reiniciar direto na BIOS (modo legado). Reinicie e aperte {VendorKeys} ao ligar.",
                    "OK", null);
            }
        });

    private void ShowDialog(string title, string message, string confirm, Action? onConfirm)
    {
        DialogTitle = title;
        DialogMessage = message;
        DialogConfirm = confirm;
        _dialogAction = onConfirm;
        IsDialogOpen = true;
    }

    private void ApplyPreset(Preset preset)
    {
        foreach (var t in Tweaks)
        {
            t.IsSelected = preset.Includes(t.Tweak.Level);
        }

        UpdateCounts();
    }

    private void SetSelection(Func<TweakItemViewModel, bool> selected)
    {
        foreach (var t in Tweaks)
        {
            t.IsSelected = selected(t);
        }
    }

    private void UpdateCounts()
    {
        SelectedCount = Tweaks?.Count(t => t.IsSelected) ?? 0;
    }

    private async Task RefreshAppliedAsync()
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

        // Pontuação ponderada: ajustes Seguro valem mais que Extremo; limpezas e Manual não contam.
        static int Weight(ITweak t) => t.Level switch { TweakLevel.Safe => 3, TweakLevel.Gamer => 2, TweakLevel.Extreme => 1, _ => 0 };
        var scored = Tweaks.Where(t => t.Tweak.Reversible && Weight(t.Tweak) > 0).ToList();
        var total = scored.Sum(t => Weight(t.Tweak));
        Score = total == 0 ? 0 : Math.Round(100.0 * scored.Where(t => t.IsApplied).Sum(t => Weight(t.Tweak)) / total);
        AppliedCount = Tweaks.Count(t => t.IsApplied);
    }

    private void ShowBios(BiosReport report)
    {
        var hw = report.Hardware;
        VendorKeys = report.Vendor.EnterKeys;
        Hardware.Clear();
        Hardware.Add(new SpecRow("Placa-mãe", $"{hw.BoardManufacturer} {hw.BoardProduct}".Trim()));
        Hardware.Add(new SpecRow("Processador", hw.CpuName));
        Hardware.Add(new SpecRow("Memória", hw.Memory.Count == 0
            ? "—"
            : $"{hw.Memory.Sum(m => m.CapacityBytes) >> 30} GB {hw.Memory[0].Generation} · {hw.Memory.Count}x · {hw.Memory.Min(m => m.ConfiguredSpeedMts)} MT/s"));
        Hardware.Add(new SpecRow("Vídeo", hw.Gpus.Count == 0 ? "—" : string.Join("\n", hw.Gpus)));
        Hardware.Add(new SpecRow("BIOS", $"{hw.BiosVersion}{(hw.BiosReleaseDate is { } d ? $" · {d:MM/yyyy}" : "")}"));
        Hardware.Add(new SpecRow("Firmware", hw.IsUefi switch { true => "UEFI", false => "Legado (CSM)", _ => "—" }));
        Hardware.Add(new SpecRow("Secure Boot", hw.SecureBootEnabled switch { true => "Ativado", false => "Desativado", _ => "—" }));
        Hardware.Add(new SpecRow("Entrar na BIOS", report.Vendor.EnterKeys));

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

        BiosActionCount = report.Items.Count(i => i.Status == AdviceStatus.ActionNeeded);
    }

    private void ReloadBackups()
    {
        Backups.Clear();
        foreach (var session in _backups.LoadAll())
        {
            Backups.Add(new BackupItemViewModel(session));
        }

        LastRun = Backups.FirstOrDefault() is { } last
            ? last.Session.CreatedAt.Date == DateTime.Today ? $"Hoje, {last.Session.CreatedAt:HH:mm}" : last.Session.CreatedAt.ToString("dd/MM/yyyy")
            : "Nunca";
    }

    private static string WindowsName() =>
        Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10";

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
