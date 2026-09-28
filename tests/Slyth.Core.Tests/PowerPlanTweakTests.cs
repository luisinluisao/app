using Slyth.Core.Backup;
using Slyth.Core.Optimization;
using Slyth.Core.Platform;
using Slyth.Core.Tweaks;

namespace Slyth.Core.Tests;

public class PowerPlanTweakTests
{
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";

    [Fact]
    public void Activates_high_performance_and_revert_restores_previous_plan_with_localized_output()
    {
        var commands = new FakeCommands
        {
            Handler = cmd => cmd switch
            {
                "powercfg /getactivescheme" => new(0, $"GUID do Esquema de Energia: {Balanced}  (Equilibrado)", ""),
                "powercfg /list" => new(0,
                    $"GUID do Esquema de Energia: {Balanced}  (Equilibrado) *\n" +
                    $"GUID do Esquema de Energia: {PowerPlanTweak.HighPerformance}  (Alto desempenho)", ""),
                _ => new(0, "", ""),
            },
        };
        var store = new MemoryBackupStore();
        var optimizer = new Optimizer(new FakeRegistry(), commands, store);

        var report = optimizer.Run([new PowerPlanTweak()]);

        Assert.Equal(TweakStatus.Applied, report.Results.Single().Status);
        Assert.Contains($"powercfg /setactive {PowerPlanTweak.HighPerformance}", commands.Calls);
        var change = store.Saved[report.BackupId!].Changes.Single();
        Assert.Equal(ChangeKind.PowerScheme, change.Kind);
        Assert.Equal(Balanced, change.PreviousPowerScheme);

        optimizer.Revert(store.Saved[report.BackupId!]);
        Assert.Equal($"powercfg /setactive {Balanced}", commands.Calls[^1]);
    }

    [Fact]
    public void Duplicates_hidden_high_performance_plan_when_missing()
    {
        const string copy = "11111111-2222-3333-4444-555555555555";
        var commands = new FakeCommands
        {
            Handler = cmd => cmd switch
            {
                "powercfg /getactivescheme" => new(0, $"Power Scheme GUID: {Balanced}  (Balanced)", ""),
                "powercfg /list" => new(0, $"Power Scheme GUID: {Balanced}  (Balanced) *", ""),
                _ when cmd.StartsWith("powercfg /duplicatescheme") => new(0, $"Power Scheme GUID: {copy}  (High performance)", ""),
                _ => new(0, "", ""),
            },
        };

        new Optimizer(new FakeRegistry(), commands, new MemoryBackupStore()).Run([new PowerPlanTweak()]);

        Assert.Contains(commands.Calls, c => c.StartsWith($"powercfg /changename {copy}"));
        Assert.Contains($"powercfg /setactive {copy}", commands.Calls);
    }

    [Fact]
    public void Is_applied_when_own_plan_is_active()
    {
        var commands = new FakeCommands
        {
            Handler = _ => new(0, $"Power Scheme GUID: 11111111-2222-3333-4444-555555555555  ({PowerPlanTweak.OwnSchemeName})", ""),
        };
        var context = new TweakContext(new FakeRegistry(), commands, new BackupSession());

        Assert.True(new PowerPlanTweak().IsApplied(context));
    }
}
