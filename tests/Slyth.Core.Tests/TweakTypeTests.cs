using Slyth.Core.Backup;
using Slyth.Core.Optimization;
using Slyth.Core.Platform;
using Slyth.Core.Tweaks;

namespace Slyth.Core.Tests;

public class TweakTypeTests
{
    private const string Services = @"SYSTEM\CurrentControlSet\Services";

    [Fact]
    public void Service_tweak_disables_installed_services_skips_missing_ones_and_reverts()
    {
        var registry = new FakeRegistry();
        registry.SetValue(RegistryRoot.LocalMachine, $@"{Services}\DiagTrack", "Start", RegistryValue.Dword(2));
        var commands = new FakeCommands();
        var store = new MemoryBackupStore();
        var optimizer = new Optimizer(registry, commands, store);
        var tweak = new ServiceTweak { Id = "s", Name = "s", Description = "", Services = ["DiagTrack", "NotInstalled"] };

        var report = optimizer.Run([tweak]);

        Assert.Equal("1 serviço(s) desativado(s)", report.Results.Single().Detail);
        Assert.Equal(RegistryValue.Dword(4), registry.GetValue(RegistryRoot.LocalMachine, $@"{Services}\DiagTrack", "Start"));
        Assert.Null(registry.GetValue(RegistryRoot.LocalMachine, $@"{Services}\NotInstalled", "Start"));
        Assert.Contains("sc stop DiagTrack", commands.Calls);

        optimizer.Revert(store.Saved[report.BackupId!]);
        Assert.Equal(RegistryValue.Dword(2), registry.GetValue(RegistryRoot.LocalMachine, $@"{Services}\DiagTrack", "Start"));
    }

    [Fact]
    public void Network_tweak_touches_only_connected_adapters()
    {
        var registry = new FakeRegistry();
        var key = NetworkInterfaceTweak.InterfacesKey;
        registry.SetValue(RegistryRoot.LocalMachine, $@"{key}\{{dhcp}}", "DhcpIPAddress", RegistryValue.Str("192.168.0.10"));
        registry.SetValue(RegistryRoot.LocalMachine, $@"{key}\{{static}}", "IPAddress", new RegistryValue(RegistryValueType.MultiString, "10.0.0.5"));
        registry.SetValue(RegistryRoot.LocalMachine, $@"{key}\{{off}}", "DhcpIPAddress", RegistryValue.Str("0.0.0.0"));
        var tweak = new NetworkInterfaceTweak { Id = "n", Name = "n", Description = "", Values = [("TCPNoDelay", RegistryValue.Dword(1))] };
        var context = new TweakContext(registry, new FakeCommands(), new BackupSession());

        Assert.False(tweak.IsApplied(context));
        Assert.Equal("2 adaptador(es) de rede", tweak.Apply(context));
        Assert.True(tweak.IsApplied(context));
        Assert.Null(registry.GetValue(RegistryRoot.LocalMachine, $@"{key}\{{off}}", "TCPNoDelay"));
    }

    [Fact]
    public void Command_tweak_records_its_undo_command_and_fails_on_nonzero_exit()
    {
        var commands = new FakeCommands();
        var store = new MemoryBackupStore();
        var optimizer = new Optimizer(new FakeRegistry(), commands, store);
        var tweak = new CommandTweak
        {
            Id = "h", Name = "h", Description = "", Category = TweakCategory.Performance,
            Command = ("powercfg", "/hibernate off"),
            Revert = ("powercfg", "/hibernate on"),
        };

        var report = optimizer.Run([tweak]);
        optimizer.Revert(store.Saved[report.BackupId!]);
        Assert.Equal("powercfg /hibernate on", commands.Calls[^1]);

        commands.Handler = _ => new CommandResult(1, "", "acesso negado");
        var failed = optimizer.Run([tweak]);
        Assert.Equal(TweakStatus.Failed, failed.Results.Single().Status);
        Assert.Equal("acesso negado", failed.Results.Single().Detail);
    }

    [Theory]
    [InlineData(Preset.Safe, TweakLevel.Gamer, false)]
    [InlineData(Preset.Gamer, TweakLevel.Gamer, true)]
    [InlineData(Preset.Gamer, TweakLevel.Extreme, false)]
    [InlineData(Preset.Extreme, TweakLevel.Extreme, true)]
    [InlineData(Preset.Extreme, TweakLevel.Manual, false)]
    public void Presets_include_lower_levels_and_never_manual(Preset preset, TweakLevel level, bool expected) =>
        Assert.Equal(expected, preset.Includes(level));
}
