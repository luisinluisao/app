using PcOptimizer.Core.Backup;
using PcOptimizer.Core.Tweaks;

namespace PcOptimizer.Core.Tests;

public class TempCleanupTweakTests
{
    [Fact]
    public void Deletes_only_old_files_and_empty_folders()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pcopt-" + Guid.NewGuid());
        var sub = Directory.CreateDirectory(Path.Combine(dir, "old-folder")).FullName;
        try
        {
            var oldFile = Path.Combine(sub, "old.tmp");
            var newFile = Path.Combine(dir, "new.tmp");
            File.WriteAllBytes(oldFile, new byte[2048]);
            File.WriteAllBytes(newFile, new byte[10]);
            File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-3));

            var tweak = new TempCleanupTweak([dir], TimeSpan.FromDays(1));
            var detail = tweak.Apply(new TweakContext(new FakeRegistry(), new FakeCommands(), new BackupSession()));

            Assert.False(File.Exists(oldFile));
            Assert.False(Directory.Exists(sub));
            Assert.True(File.Exists(newFile));
            Assert.Equal("1 arquivos removidos, 2 KB liberados", detail);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
