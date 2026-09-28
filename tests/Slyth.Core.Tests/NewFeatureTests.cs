using Slyth.Core.Backup;
using Slyth.Core.Network;
using Slyth.Core.Optimization;
using Slyth.Core.Platform;
using Slyth.Core.Startup;
using Slyth.Core.Tweaks;

namespace Slyth.Core.Tests;

public class NewFeatureTests
{
    [Fact]
    public void Scheduled_task_tweak_disables_enabled_tasks_skips_missing_and_undoes()
    {
        var disabled = new HashSet<string>();
        var commands = new FakeCommands
        {
            Handler = cmd =>
            {
                if (cmd.Contains("Missing"))
                {
                    return new CommandResult(1, "", "not found");
                }

                if (cmd.StartsWith("schtasks /Change") && cmd.EndsWith("/Disable"))
                {
                    disabled.Add(cmd);
                }

                var enabled = disabled.Count == 0 ? "true" : "false";
                return new CommandResult(0, $"<Task><Settings><Enabled>{enabled}</Enabled></Settings></Task>", "");
            },
        };
        var store = new MemoryBackupStore();
        var optimizer = new Optimizer(new FakeRegistry(), commands, store);
        var tweak = new ScheduledTaskTweak { Id = "t", Name = "t", Description = "", Tasks = [@"\A\Appraiser", @"\A\Missing"] };

        var report = optimizer.Run([tweak]);

        Assert.Equal("1 tarefa(s) desativada(s)", report.Results.Single().Detail);
        Assert.Contains("schtasks /Change /TN \"\\A\\Appraiser\" /Disable", commands.Calls);
        Assert.DoesNotContain(commands.Calls, c => c.Contains("Missing") && c.Contains("/Disable"));

        optimizer.Revert(store.Saved[report.BackupId!]);
        Assert.Equal("schtasks /Change /TN \"\\A\\Appraiser\" /Enable", commands.Calls[^1]);
    }

    [Fact]
    public void Nic_power_tweak_only_touches_physical_adapters_and_existing_options()
    {
        var registry = new FakeRegistry();
        var key = NetworkAdapterPowerTweak.ClassKey;
        void Adapter(string id, string type, string desc)
        {
            registry.SetValue(RegistryRoot.LocalMachine, $@"{key}\{id}", "*IfType", RegistryValue.Dword(int.Parse(type)));
            registry.SetValue(RegistryRoot.LocalMachine, $@"{key}\{id}", "NetCfgInstanceId", RegistryValue.Str("{guid}"));
            registry.SetValue(RegistryRoot.LocalMachine, $@"{key}\{id}", "DriverDesc", RegistryValue.Str(desc));
        }

        Adapter("0001", "6", "Realtek PCIe GbE Family Controller");
        registry.SetValue(RegistryRoot.LocalMachine, $@"{key}\0001", "*EEE", RegistryValue.Str("1"));
        Adapter("0002", "6", "Hyper-V Virtual Ethernet Adapter");
        Adapter("0003", "71", "Intel(R) Wi-Fi 6 AX201");
        registry.SetValue(RegistryRoot.LocalMachine, $@"{key}\Properties", "x", RegistryValue.Str("y"));
        var context = new TweakContext(registry, new FakeCommands(), new BackupSession());
        var tweak = new NetworkAdapterPowerTweak();

        Assert.False(tweak.IsApplied(context));
        Assert.Equal("2 placa(s), 1 opção(ões) de economia desligada(s)", tweak.Apply(context));
        Assert.True(tweak.IsApplied(context));
        Assert.Equal(RegistryValue.Str("0"), registry.GetValue(RegistryRoot.LocalMachine, $@"{key}\0001", "*EEE"));
        Assert.Null(registry.GetValue(RegistryRoot.LocalMachine, $@"{key}\0003", "*EEE"));
        Assert.Null(registry.GetValue(RegistryRoot.LocalMachine, $@"{key}\0002", "PnPCapabilities"));
        Assert.Equal(RegistryValue.Dword(24), registry.GetValue(RegistryRoot.LocalMachine, $@"{key}\0003", "PnPCapabilities"));
    }

    [Fact]
    public void Startup_manager_lists_entries_and_toggles_like_task_manager()
    {
        var registry = new FakeRegistry();
        const string run = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        registry.SetValue(RegistryRoot.CurrentUser, run, "Discord", RegistryValue.Str("\"C:\\Discord\\Update.exe\" --processStart Discord.exe"));
        registry.SetValue(RegistryRoot.LocalMachine, run, "Steam", RegistryValue.Str("C:\\Steam\\steam.exe -silent"));
        registry.SetValue(RegistryRoot.LocalMachine, approved, "Steam", new RegistryValue(RegistryValueType.Binary, "030000000000000000000000"));
        var manager = new StartupManager(registry, "", "");

        var entries = manager.List();

        var discord = entries.Single(e => e.Name == "Discord");
        var steam = entries.Single(e => e.Name == "Steam");
        Assert.True(discord.Enabled);
        Assert.False(steam.Enabled);
        Assert.Equal("C:\\Discord\\Update.exe", discord.ExecutablePath);
        Assert.Equal("C:\\Steam\\steam.exe", steam.ExecutablePath);

        manager.SetEnabled(discord, false);
        manager.SetEnabled(steam, true);

        Assert.False(manager.List().Single(e => e.Name == "Discord").Enabled);
        Assert.True(manager.List().Single(e => e.Name == "Steam").Enabled);
        Assert.StartsWith("03", registry.GetValue(RegistryRoot.CurrentUser, approved, "Discord")!.Data);
    }

    [Fact]
    public void Dns_query_is_a_valid_rfc1035_packet()
    {
        var packet = DnsBenchmark.BuildQuery(0x1234, "discord.com");

        Assert.Equal(new byte[] { 0x12, 0x34, 0x01, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0 }, packet[..12]);
        Assert.Equal(7, packet[12]);
        Assert.Equal("discord", System.Text.Encoding.ASCII.GetString(packet, 13, 7));
        Assert.Equal(new byte[] { 0, 0, 1, 0, 1 }, packet[^5..]);

        var response = (byte[])packet.Clone();
        response[2] |= 0x80;
        Assert.True(DnsBenchmark.IsResponseTo(response, 0x1234));
        Assert.False(DnsBenchmark.IsResponseTo(packet, 0x1234));
        Assert.False(DnsBenchmark.IsResponseTo(response, 0x9999));
    }

    [Fact]
    public void Latency_result_computes_jitter_and_loss()
    {
        var result = LatencyResult.From([10, 20, null, 10, 20]);

        Assert.Equal(15, result.AverageMs);
        Assert.Equal(10, result.JitterMs);
        Assert.Equal(20, result.LossPercent);
        Assert.Contains("Perda", result.Verdict);
    }

    [Fact]
    public void Dns_tweak_sets_and_resets_name_servers()
    {
        var registry = new FakeRegistry();
        var iface = $@"{NetworkInterfaceTweak.InterfacesKey}\{{a}}";
        registry.SetValue(RegistryRoot.LocalMachine, iface, "DhcpIPAddress", RegistryValue.Str("192.168.1.5"));
        var store = new MemoryBackupStore();
        var optimizer = new Optimizer(registry, new FakeCommands(), store);

        var report = optimizer.Run([DnsTweaks.Create(DnsProvider.Known[0])]);
        Assert.Equal(RegistryValue.Str("1.1.1.1,1.0.0.1"), registry.GetValue(RegistryRoot.LocalMachine, iface, "NameServer"));

        optimizer.Revert(store.Saved[report.BackupId!]);
        Assert.Null(registry.GetValue(RegistryRoot.LocalMachine, iface, "NameServer"));
    }
}
