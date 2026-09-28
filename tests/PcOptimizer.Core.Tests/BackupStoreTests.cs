using PcOptimizer.Core.Backup;
using PcOptimizer.Core.Platform;

namespace PcOptimizer.Core.Tests;

public class BackupStoreTests
{
    [Fact]
    public void Round_trips_sessions_newest_first_and_ignores_corrupt_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pcopt-" + Guid.NewGuid());
        try
        {
            var store = new JsonBackupStore(dir);
            var older = new BackupSession { Id = "a", CreatedAt = new DateTime(2025, 1, 1) };
            older.Changes.Add(new ChangeRecord
            {
                Kind = ChangeKind.Registry,
                TweakId = "t",
                Root = RegistryRoot.LocalMachine,
                Key = @"SOFTWARE\X",
                Name = "V",
                PreviousValue = RegistryValue.Dword(3),
            });
            store.Save(older);
            store.Save(new BackupSession { Id = "b", CreatedAt = new DateTime(2026, 1, 1) });
            File.WriteAllText(Path.Combine(dir, "broken.json"), "{ not json");

            var loaded = store.LoadAll();

            Assert.Equal(["b", "a"], loaded.Select(s => s.Id));
            Assert.Equal(RegistryValue.Dword(3), loaded[1].Changes.Single().PreviousValue);
            Assert.Equal(RegistryRoot.LocalMachine, loaded[1].Changes.Single().Root);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
