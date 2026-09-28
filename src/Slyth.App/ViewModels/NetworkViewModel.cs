using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;
using Slyth.Core.Network;
using Slyth.Core.Optimization;

namespace Slyth.App.ViewModels;

public sealed class DnsRowViewModel : ObservableObject
{
    private string _msText = "—";
    private double _score;
    private bool _isFastest;

    public DnsRowViewModel(string name, string servers, DnsProvider? provider, bool isCurrent, Action<DnsRowViewModel> apply)
    {
        Name = name;
        Servers = servers;
        Provider = provider;
        IsCurrent = isCurrent;
        ApplyCommand = new RelayCommand(() => apply(this), () => Provider is not null);
    }

    public string Name { get; }

    public string Servers { get; }

    public DnsProvider? Provider { get; }

    public bool IsCurrent { get; }

    public bool CanApply => Provider is not null && !IsCurrent;

    public double? Milliseconds { get; set; }

    public RelayCommand ApplyCommand { get; }

    public string MsText { get => _msText; set => Set(ref _msText, value); }

    public double Score { get => _score; set => Set(ref _score, value); }

    public bool IsFastest { get => _isFastest; set => Set(ref _isFastest, value); }
}

public sealed class NetworkViewModel : ObservableObject
{
    private const string PingHost = "1.1.1.1";
    private const int PingCount = 30;

    private readonly Optimizer _optimizer;
    private readonly Func<Task> _afterChange;
    private readonly Action<string, string, string, Action?> _dialog;
    private readonly Queue<double> _pingHistory = new(Enumerable.Repeat(0d, PingCount));

    private bool _isTesting;
    private bool _hasResult;
    private string _ping = "—", _jitter = "—", _loss = "—", _verdict = "Teste a conexão para ver ping, variação e perda de pacotes.";
    private PointCollection _pingLine = [];
    private string _adapterName = "—", _adapterKind = "—", _linkSpeed = "—", _localIp = "—", _currentDns = "—";

    public NetworkViewModel(Optimizer optimizer, Func<Task> afterChange, Action<string, string, string, Action?> dialog)
    {
        _optimizer = optimizer;
        _afterChange = afterChange;
        _dialog = dialog;
        TestCommand = new RelayCommand(async () => await TestAsync(), () => !IsTesting);
        ResetDnsCommand = new RelayCommand(() => Apply(null, "DNS automático"), () => !IsTesting);
    }

    public ObservableCollection<DnsRowViewModel> DnsRows { get; } = [];

    public RelayCommand TestCommand { get; }

    public RelayCommand ResetDnsCommand { get; }

    public bool IsTesting { get => _isTesting; private set => Set(ref _isTesting, value); }

    public bool HasResult { get => _hasResult; private set => Set(ref _hasResult, value); }

    public string Ping { get => _ping; private set => Set(ref _ping, value); }

    public string Jitter { get => _jitter; private set => Set(ref _jitter, value); }

    public string Loss { get => _loss; private set => Set(ref _loss, value); }

    public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }

    public PointCollection PingLine { get => _pingLine; private set => Set(ref _pingLine, value); }

    public string AdapterName { get => _adapterName; private set => Set(ref _adapterName, value); }

    public string AdapterKind { get => _adapterKind; private set => Set(ref _adapterKind, value); }

    public string LinkSpeed { get => _linkSpeed; private set => Set(ref _linkSpeed, value); }

    public string LocalIp { get => _localIp; private set => Set(ref _localIp, value); }

    public string CurrentDns { get => _currentDns; private set => Set(ref _currentDns, value); }

    public void LoadAdapterInfo()
    {
        if (Services.NetworkInfo.ActiveInterface() is not { } active)
        {
            AdapterName = "Sem conexão";
            return;
        }

        AdapterName = active.Nic.Description;
        AdapterKind = active.Nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "Cabo (Ethernet)";
        LinkSpeed = active.Nic.Speed >= 1_000_000_000 ? $"{active.Nic.Speed / 1_000_000_000.0:0.#} Gbps" : $"{active.Nic.Speed / 1_000_000} Mbps";
        LocalIp = active.Props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "—";
        var dns = active.Props.DnsAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToList();
        CurrentDns = dns.Count == 0 ? "—" : string.Join(", ", dns);
        BuildDnsRows(dns);
    }

    private void BuildDnsRows(IReadOnlyList<IPAddress> currentDns)
    {
        DnsRows.Clear();
        var current = currentDns.FirstOrDefault()?.ToString();
        var knownCurrent = DnsProvider.Known.FirstOrDefault(p => p.Primary == current || p.Secondary == current);
        if (current is not null && knownCurrent is null)
        {
            DnsRows.Add(new DnsRowViewModel("Atual (operadora)", current, null, true, _ => { }));
        }

        foreach (var provider in DnsProvider.Known)
        {
            DnsRows.Add(new DnsRowViewModel(provider.Name, $"{provider.Primary} · {provider.Secondary}", provider, provider == knownCurrent,
                row => Apply(row.Provider, row.Name)));
        }
    }

    private async Task TestAsync()
    {
        IsTesting = true;
        Verdict = "Medindo ping...";
        try
        {
            var progress = new Progress<double?>(rtt =>
            {
                _pingHistory.Dequeue();
                _pingHistory.Enqueue(rtt ?? 150);
                var line = new PointCollection(PingCount);
                var i = 0;
                foreach (var v in _pingHistory)
                {
                    line.Add(new Point(i++ * 100.0 / (PingCount - 1), 100 - Math.Min(v, 150) / 150 * 100));
                }

                line.Freeze();
                PingLine = line;
                if (rtt is { } ms)
                {
                    Ping = $"{ms:0}";
                }
            });

            var result = await LatencyTest.RunAsync(PingHost, PingCount, progress);
            Ping = result.LossPercent >= 100 ? "—" : $"{result.AverageMs:0}";
            Jitter = $"{result.JitterMs:0.0}";
            Loss = $"{result.LossPercent:0}";
            Verdict = result.Verdict;

            Verdict = result.Verdict + " · comparando DNS...";
            await MeasureDnsAsync();
            Verdict = result.Verdict;
            HasResult = true;
        }
        catch (Exception e)
        {
            Verdict = $"Não foi possível testar: {e.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    private async Task MeasureDnsAsync()
    {
        var tasks = DnsRows.Select(async row =>
        {
            var ip = IPAddress.Parse(row.Servers.Split(' ')[0]);
            row.Milliseconds = await DnsBenchmark.MeasureAsync(ip);
        }).ToList();
        await Task.WhenAll(tasks);

        var measured = DnsRows.Where(r => r.Milliseconds is not null).ToList();
        var fastest = measured.Count == 0 ? 0 : measured.Min(r => r.Milliseconds!.Value);
        foreach (var row in DnsRows)
        {
            row.MsText = row.Milliseconds is { } ms ? $"{ms:0} ms" : "sem resposta";
            row.Score = row.Milliseconds is { } m && m > 0 ? 100 * fastest / m : 0;
            row.IsFastest = row.Milliseconds is { } f && f == fastest;
        }
    }

    private void Apply(DnsProvider? provider, string name) => _dialog(
        provider is null ? "Voltar ao DNS automático?" : $"Usar o DNS {name}?",
        "A troca fica salva no Histórico e pode ser desfeita. Vale depois de reiniciar o PC.",
        provider is null ? "Voltar ao automático" : "Usar este DNS",
        async () =>
        {
            var report = await Task.Run(() => _optimizer.Run([DnsTweaks.Create(provider)]));
            var result = report.Results.Single();
            _dialog(result.Status == TweakStatus.Failed ? "Não foi possível" : "Pronto",
                result.Status == TweakStatus.Failed ? result.Detail ?? "" : "DNS alterado. Reinicie o PC para começar a usar.",
                "OK", null);
            await _afterChange();
        });
}
