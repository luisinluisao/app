using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Slyth.App.Interop;
using Slyth.App.ViewModels;
using Slyth.Core;

namespace Slyth.App.Services;

/// <summary>Leituras ao vivo de CPU, memória, disco e processos para o painel.</summary>
public sealed class SystemMonitor : ObservableObject
{
    private const int HistoryLength = 60;
    private readonly Queue<double> _cpu = new(Enumerable.Repeat(0d, HistoryLength));
    private readonly Queue<double> _ram = new(Enumerable.Repeat(0d, HistoryLength));
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private long _lastIdle, _lastKernel, _lastUser;
    private int _tick;

    private double _cpuPercent, _ramPercent, _diskPercent;
    private string _ramText = "", _diskText = "", _uptimeText = "";
    private int _processCount;
    private PointCollection _cpuLine = [], _ramLine = [];

    public SystemMonitor()
    {
        NativeMethods.GetSystemTimes(out _lastIdle, out _lastKernel, out _lastUser);
        _timer.Tick += (_, _) => Sample();
    }

    public double CpuPercent { get => _cpuPercent; private set => Set(ref _cpuPercent, value); }

    public double RamPercent { get => _ramPercent; private set => Set(ref _ramPercent, value); }

    public double DiskPercent { get => _diskPercent; private set => Set(ref _diskPercent, value); }

    public string RamText { get => _ramText; private set => Set(ref _ramText, value); }

    public string DiskText { get => _diskText; private set => Set(ref _diskText, value); }

    public string UptimeText { get => _uptimeText; private set => Set(ref _uptimeText, value); }

    public int ProcessCount { get => _processCount; private set => Set(ref _processCount, value); }

    public PointCollection CpuLine { get => _cpuLine; private set => Set(ref _cpuLine, value); }

    public PointCollection RamLine { get => _ramLine; private set => Set(ref _ramLine, value); }

    public void Start()
    {
        Sample();
        _timer.Start();
    }

    private void Sample()
    {
        if (NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            var total = kernel - _lastKernel + (user - _lastUser);
            if (total > 0)
            {
                CpuPercent = Math.Round(Math.Clamp(100.0 * (1 - (double)(idle - _lastIdle) / total), 0, 100));
            }

            (_lastIdle, _lastKernel, _lastUser) = (idle, kernel, user);
        }

        var mem = new NativeMethods.MemoryStatusEx { Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MemoryStatusEx>() };
        if (NativeMethods.GlobalMemoryStatusEx(ref mem))
        {
            RamPercent = mem.MemoryLoad;
            RamText = $"{(mem.TotalPhys - mem.AvailPhys) / (double)(1L << 30):0.0} de {mem.TotalPhys / (double)(1L << 30):0} GB em uso";
        }

        CpuLine = Push(_cpu, CpuPercent);
        RamLine = Push(_ram, RamPercent);

        // Leituras mais caras a cada 5 segundos.
        if (_tick++ % 5 == 0)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
                DiskPercent = 100.0 * (drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize;
                DiskText = $"{Bytes.Format(drive.TotalFreeSpace)} livres de {Bytes.Format(drive.TotalSize)}";
            }
            catch (IOException)
            {
            }

            ProcessCount = Process.GetProcesses().Length;
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            UptimeText = up.TotalDays >= 1 ? $"Ligado há {(int)up.TotalDays}d {up.Hours}h" : $"Ligado há {up.Hours}h {up.Minutes:00}min";
        }
    }

    /// <summary>Pontos em um espaço 100×100; o <see cref="Controls.Sparkline"/> escala para o tamanho real.</summary>
    private static PointCollection Push(Queue<double> history, double value)
    {
        history.Dequeue();
        history.Enqueue(value);
        var line = new PointCollection(HistoryLength);
        var i = 0;
        foreach (var v in history)
        {
            line.Add(new Point(i++ * 100.0 / (HistoryLength - 1), 100 - Math.Clamp(v, 0, 100)));
        }

        line.Freeze();
        return line;
    }
}
