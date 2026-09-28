namespace Slyth.Core.Bios;

public sealed record MemoryModule(long CapacityBytes, int RatedSpeedMts, int ConfiguredSpeedMts, int SmbiosMemoryType)
{
    public string Generation => SmbiosMemoryType switch
    {
        24 => "DDR3",
        26 => "DDR4",
        34 => "DDR5",
        _ => "",
    };
}

public sealed record HardwareInfo
{
    public string BoardManufacturer { get; init; } = "";

    public string BoardProduct { get; init; } = "";

    public string SystemManufacturer { get; init; } = "";

    public string SystemModel { get; init; } = "";

    public string BiosVersion { get; init; } = "";

    public DateTime? BiosReleaseDate { get; init; }

    public bool? IsUefi { get; init; }

    public bool? SecureBootEnabled { get; init; }

    public bool? VirtualizationEnabled { get; init; }

    public bool IsLaptop { get; init; }

    public string CpuName { get; init; } = "";

    public IReadOnlyList<MemoryModule> Memory { get; init; } = [];

    public IReadOnlyList<string> Gpus { get; init; } = [];
}

public interface IHardwareProbe
{
    HardwareInfo Probe();
}
