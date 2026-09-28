using System.Management;
using System.Runtime.Versioning;
using PcOptimizer.Core.Platform;

namespace PcOptimizer.Core.Bios;

/// <summary>Lê o hardware e a BIOS via WMI. Apenas leitura: nada é alterado na BIOS.</summary>
[SupportedOSPlatform("windows")]
public sealed class WmiHardwareProbe(IRegistry registry) : IHardwareProbe
{
    private static readonly int[] LaptopChassis = [8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32];

    public HardwareInfo Probe()
    {
        var board = First("SELECT Manufacturer, Product FROM Win32_BaseBoard");
        var system = First("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
        var bios = First("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");
        var cpu = First("SELECT Name, VirtualizationFirmwareEnabled FROM Win32_Processor");
        var chassis = First("SELECT ChassisTypes FROM Win32_SystemEnclosure");

        return new HardwareInfo
        {
            BoardManufacturer = Str(board, "Manufacturer"),
            BoardProduct = Str(board, "Product"),
            SystemManufacturer = Str(system, "Manufacturer"),
            SystemModel = Str(system, "Model"),
            BiosVersion = Str(bios, "SMBIOSBIOSVersion"),
            BiosReleaseDate = bios?["ReleaseDate"] is string date ? SafeDate(date) : null,
            CpuName = Str(cpu, "Name"),
            VirtualizationEnabled = cpu?["VirtualizationFirmwareEnabled"] as bool?,
            IsLaptop = chassis?["ChassisTypes"] is ushort[] types && types.Any(t => LaptopChassis.Contains(t)),
            IsUefi = registry.GetValue(RegistryRoot.LocalMachine, @"SYSTEM\CurrentControlSet\Control", "PEFirmwareType") is { } fw
                ? fw.Data == "2"
                : null,
            SecureBootEnabled = registry.GetValue(RegistryRoot.LocalMachine, @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled") is { } sb
                ? sb.Data == "1"
                : null,
            Memory = All("SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType FROM Win32_PhysicalMemory")
                .Select(m => new MemoryModule(
                    Convert.ToInt64(m["Capacity"] ?? 0L),
                    Convert.ToInt32(m["Speed"] ?? 0),
                    Convert.ToInt32(m["ConfiguredClockSpeed"] ?? 0),
                    Convert.ToInt32(m["SMBIOSMemoryType"] ?? 0)))
                .ToList(),
            Gpus = All("SELECT Name FROM Win32_VideoController").Select(g => Str(g, "Name")).Where(n => n.Length > 0).ToList(),
        };
    }

    private static List<ManagementBaseObject> All(string query)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            return searcher.Get().Cast<ManagementBaseObject>().ToList();
        }
        catch (ManagementException)
        {
            return [];
        }
    }

    private static ManagementBaseObject? First(string query) => All(query).FirstOrDefault();

    private static string Str(ManagementBaseObject? obj, string property)
    {
        try
        {
            return obj?[property]?.ToString()?.Trim() ?? "";
        }
        catch (ManagementException)
        {
            return "";
        }
    }

    private static DateTime? SafeDate(string dmtf)
    {
        try
        {
            return ManagementDateTimeConverter.ToDateTime(dmtf);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
