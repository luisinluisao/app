using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Slyth.App.Interop;
using Slyth.Core.Network;
using Slyth.Core.Performance;
using Slyth.Core.Platform;
using Slyth.Core.Startup;

namespace Slyth.App.Services;

public sealed class WindowsPerformanceProbe(ICommandRunner commands, StartupManager startup) : IPerformanceProbe
{
    public double? BootSeconds()
    {
        var result = commands.Run("wevtutil", BootTime.WevtutilArgs);
        var lastBootUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
        return result.Success ? BootTime.Parse(result.Output, lastBootUtc) : null;
    }

    public double? RamUsedGb()
    {
        var mem = new NativeMethods.MemoryStatusEx { Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>() };
        return NativeMethods.GlobalMemoryStatusEx(ref mem) ? (mem.TotalPhys - mem.AvailPhys) / (double)(1L << 30) : null;
    }

    public async Task<double?> IdleCpuPercentAsync(TimeSpan duration, CancellationToken cancel)
    {
        if (!NativeMethods.GetSystemTimes(out var idle1, out var kernel1, out var user1))
        {
            return null;
        }

        await Task.Delay(duration, cancel);
        if (!NativeMethods.GetSystemTimes(out var idle2, out var kernel2, out var user2))
        {
            return null;
        }

        var total = kernel2 - kernel1 + (user2 - user1);
        return total <= 0 ? null : Math.Clamp(100.0 * (1 - (double)(idle2 - idle1) / total), 0, 100);
    }

    public int? Processes() => Process.GetProcesses().Length;

    public int? RunningServices()
    {
        // Saída é só um número, igual em qualquer idioma do Windows.
        var result = commands.Run("powershell", "-NoProfile -Command \"(Get-Service | Where-Object Status -eq 'Running').Count\"");
        return result.Success && int.TryParse(result.Output.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    public int? StartupApps() => startup.List().Count(e => e.Enabled);

    public async Task<LatencyResult?> LatencyAsync(CancellationToken cancel) =>
        await LatencyTest.RunAsync("1.1.1.1", 20, cancel: cancel);

    public async Task<double?> DnsMsAsync(CancellationToken cancel) =>
        NetworkInfo.CurrentDns().FirstOrDefault() is { } dns ? await DnsBenchmark.MeasureAsync(dns, cancel) : null;

    public double? DiskFreeGb()
    {
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        return drive.TotalFreeSpace / (double)(1L << 30);
    }
}
