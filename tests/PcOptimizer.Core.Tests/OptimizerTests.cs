using PcOptimizer.Core.Optimization;
using PcOptimizer.Core.Platform;
using PcOptimizer.Core.Tweaks;

namespace PcOptimizer.Core.Tests;

public class OptimizerTests
{
    private static RegistryTweak Tweak(string id, params RegistrySetting[] settings) => new()
    {
        Id = id,
        Name = id,
        Description = "",
        Category = TweakCategory.Performance,
        Settings = settings,
    };

    private sealed class ThrowingTweak(RegistrySetting before) : ITweak
    {
        public string Id => "boom";
        public string Name => "boom";
        public string Description => "";
        public TweakCategory Category => TweakCategory.Performance;
        public bool Recommended => false;
        public bool RequiresRestart => false;
        public bool Reversible => true;
        public bool IsApplied(TweakContext context) => false;

        public string? Apply(TweakContext context)
        {
            context.SetRegistry(before.Root, before.Key, before.Name, before.Value);
            throw new InvalidOperationException("falhou");
        }
    }

    [Fact]
    public void Apply_then_revert_restores_previous_values_and_removes_created_ones()
    {
        var registry = new FakeRegistry();
        registry.SetValue(RegistryRoot.CurrentUser, "k", "existing", RegistryValue.Dword(5));
        var store = new MemoryBackupStore();
        var optimizer = new Optimizer(registry, new FakeCommands(), store);

        var report = optimizer.Run([Tweak("t",
            new(RegistryRoot.CurrentUser, "k", "existing", RegistryValue.Dword(1)),
            new(RegistryRoot.CurrentUser, "k", "created", RegistryValue.Str("x")))]);

        Assert.Equal(1, report.AppliedCount);
        Assert.Equal(RegistryValue.Dword(1), registry.GetValue(RegistryRoot.CurrentUser, "k", "existing"));

        var revert = optimizer.Revert(store.Saved[report.BackupId!]);

        Assert.Empty(revert.Errors);
        Assert.Equal(RegistryValue.Dword(5), registry.GetValue(RegistryRoot.CurrentUser, "k", "existing"));
        Assert.Null(registry.GetValue(RegistryRoot.CurrentUser, "k", "created"));
        Assert.NotNull(store.Saved[report.BackupId!].RevertedAt);
    }

    [Fact]
    public void Already_applied_tweak_is_skipped_and_produces_no_backup()
    {
        var registry = new FakeRegistry();
        registry.SetValue(RegistryRoot.CurrentUser, "k", "v", RegistryValue.Dword(1));
        var store = new MemoryBackupStore();

        var report = new Optimizer(registry, new FakeCommands(), store)
            .Run([Tweak("t", new RegistrySetting(RegistryRoot.CurrentUser, "k", "v", RegistryValue.Dword(1)))]);

        Assert.Equal(TweakStatus.AlreadyApplied, report.Results.Single().Status);
        Assert.Null(report.BackupId);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public void Failed_tweak_is_rolled_back_and_does_not_stop_the_others()
    {
        var registry = new FakeRegistry();
        registry.SetValue(RegistryRoot.LocalMachine, "a", "v", RegistryValue.Dword(7));
        var store = new MemoryBackupStore();

        var report = new Optimizer(registry, new FakeCommands(), store).Run([
            new ThrowingTweak(new(RegistryRoot.LocalMachine, "a", "v", RegistryValue.Dword(0))),
            Tweak("ok", new RegistrySetting(RegistryRoot.LocalMachine, "b", "v", RegistryValue.Dword(1))),
        ]);

        Assert.Equal(TweakStatus.Failed, report.Results[0].Status);
        Assert.Equal("falhou", report.Results[0].Detail);
        Assert.Equal(TweakStatus.Applied, report.Results[1].Status);
        Assert.Equal(RegistryValue.Dword(7), registry.GetValue(RegistryRoot.LocalMachine, "a", "v"));
        Assert.DoesNotContain(store.Saved[report.BackupId!].Changes, c => c.TweakId == "boom");
    }

    [Fact]
    public void Restart_flag_and_restore_point_are_reported()
    {
        var restore = new FakeRestorePoints();
        var tweak = new RegistryTweak
        {
            Id = "r",
            Name = "r",
            Description = "",
            Category = TweakCategory.Gaming,
            RequiresRestart = true,
            Settings = [new(RegistryRoot.LocalMachine, "k", "v", RegistryValue.Dword(2))],
        };

        var report = new Optimizer(new FakeRegistry(), new FakeCommands(), new MemoryBackupStore(), restore).Run([tweak]);

        Assert.True(report.RestartRequired);
        Assert.True(report.RestorePointCreated);
        Assert.Equal(1, restore.Calls);
    }

    [Fact]
    public void Catalog_ids_are_unique_and_recommended_set_is_not_empty()
    {
        var all = TweakCatalog.All();
        Assert.Equal(all.Count, all.Select(t => t.Id).Distinct().Count());
        Assert.Contains(all, t => t.Recommended);
    }

    private sealed class FakeRestorePoints : IRestorePointService
    {
        public int Calls { get; private set; }

        public (bool Created, string Message) TryCreate(string description)
        {
            Calls++;
            return (true, "ok");
        }
    }
}
