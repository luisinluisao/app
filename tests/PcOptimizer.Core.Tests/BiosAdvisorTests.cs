using PcOptimizer.Core.Bios;

namespace PcOptimizer.Core.Tests;

public class BiosAdvisorTests
{
    private static readonly DateTime Now = new(2026, 9, 1);

    private static HardwareInfo Desktop(params MemoryModule[] memory) => new()
    {
        BoardManufacturer = "ASUSTeK COMPUTER INC.",
        BoardProduct = "TUF GAMING B550M-PLUS",
        BiosVersion = "3607",
        BiosReleaseDate = new DateTime(2026, 3, 1),
        IsUefi = true,
        Memory = memory,
        Gpus = ["NVIDIA GeForce RTX 4060"],
    };

    private static BiosRecommendation Item(BiosReport report, string id) => report.Items.Single(i => i.Id == id);

    [Fact]
    public void Ddr4_at_jedec_speed_needs_xmp_with_vendor_specific_steps()
    {
        var report = BiosAdvisor.Analyze(Desktop(new(8L << 30, 2133, 2133, 26), new(8L << 30, 2133, 2133, 26)), Now);

        var memory = Item(report, "memory-profile");
        Assert.Equal("ASUS", report.Vendor.Name);
        Assert.Equal(AdviceStatus.ActionNeeded, memory.Status);
        Assert.Contains(memory.Steps, s => s.Contains("Ai Overclock Tuner"));
        Assert.DoesNotContain(report.Items, i => i.Id == "dual-channel");
    }

    [Fact]
    public void Ddr5_with_profile_enabled_is_ok()
    {
        var report = BiosAdvisor.Analyze(Desktop(new(16L << 30, 6000, 6000, 34), new(16L << 30, 6000, 6000, 34)), Now);

        Assert.Equal(AdviceStatus.Ok, Item(report, "memory-profile").Status);
        Assert.Equal(AdviceStatus.Ok, Item(report, "bios-update").Status);
    }

    [Fact]
    public void Single_module_old_bios_and_modern_gpu_are_flagged()
    {
        var hw = Desktop(new MemoryModule(16L << 30, 3200, 3200, 26)) with { BiosReleaseDate = new DateTime(2021, 5, 1) };

        var report = BiosAdvisor.Analyze(hw, Now);

        Assert.Equal(AdviceStatus.ActionNeeded, Item(report, "dual-channel").Status);
        Assert.Equal(AdviceStatus.ActionNeeded, Item(report, "bios-update").Status);
        Assert.Equal(AdviceStatus.CheckManually, Item(report, "resizable-bar").Status);
    }

    [Fact]
    public void Laptop_memory_is_informational_only()
    {
        var hw = Desktop(new(8L << 30, 3200, 2400, 26), new(8L << 30, 3200, 2400, 26)) with
        {
            BoardManufacturer = "LENOVO",
            IsLaptop = true,
            Gpus = ["Intel(R) UHD Graphics"],
        };

        var report = BiosAdvisor.Analyze(hw, Now);

        Assert.Equal("Lenovo", report.Vendor.Name);
        Assert.Equal(AdviceStatus.Info, Item(report, "memory-profile").Status);
        Assert.DoesNotContain(report.Items, i => i.Id == "resizable-bar");
    }

    [Theory]
    [InlineData("Micro-Star International Co., Ltd.", "MSI")]
    [InlineData("Gigabyte Technology Co., Ltd.", "Gigabyte")]
    [InlineData("HP", "HP")]
    [InlineData("Dell Inc.", "Dell")]
    [InlineData("Some Unknown Board Co.", "Genérico")]
    public void Vendor_is_recognized(string manufacturer, string expected) =>
        Assert.Equal(expected, VendorGuide.For(manufacturer).Name);
}
