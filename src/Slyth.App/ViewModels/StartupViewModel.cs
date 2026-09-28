using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Slyth.Core.Startup;

namespace Slyth.App.ViewModels;

public sealed class StartupItemViewModel : ObservableObject
{
    private readonly StartupManager _manager;
    private readonly Action _changed;
    private StartupEntry _entry;

    public StartupItemViewModel(StartupEntry entry, StartupManager manager, Action changed)
    {
        _entry = entry;
        _manager = manager;
        _changed = changed;
        Publisher = FindPublisher(entry);
    }

    public string Name => _entry.Name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? _entry.Name[..^4] : _entry.Name;

    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";

    public string Publisher { get; }

    public string Location => _entry.SourceName;

    public string Path => _entry.ExecutablePath;

    public bool IsEnabled
    {
        get => _entry.Enabled;
        set
        {
            if (value == _entry.Enabled)
            {
                return;
            }

            try
            {
                _manager.SetEnabled(_entry, value);
                _entry = _entry with { Enabled = value };
                _changed();
            }
            catch (Exception)
            {
                // Sem permissão: o interruptor volta para o estado real.
            }

            OnPropertyChanged();
        }
    }

    private static string FindPublisher(StartupEntry entry)
    {
        try
        {
            var path = entry.ExecutablePath;
            if (File.Exists(path) && !path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(info.CompanyName))
                {
                    return info.CompanyName.Trim();
                }
            }
        }
        catch (Exception)
        {
        }

        return entry.Source is StartupSource.UserFolder or StartupSource.CommonFolder ? "Atalho" : "Editor desconhecido";
    }
}

public sealed class StartupViewModel(StartupManager manager) : ObservableObject
{
    private int _enabledCount;
    private RelayCommand? _refresh;

    public ObservableCollection<StartupItemViewModel> Items { get; } = [];

    public int EnabledCount { get => _enabledCount; private set => Set(ref _enabledCount, value); }

    public RelayCommand RefreshCommand => _refresh ??= new(async () => await LoadAsync());

    public async Task LoadAsync()
    {
        var items = await Task.Run(() => manager.List().Select(e => new StartupItemViewModel(e, manager, Recount)).ToList());
        Items.Clear();
        foreach (var item in items.OrderByDescending(i => i.IsEnabled))
        {
            Items.Add(item);
        }

        Recount();
    }

    private void Recount() => EnabledCount = Items.Count(i => i.IsEnabled);
}
