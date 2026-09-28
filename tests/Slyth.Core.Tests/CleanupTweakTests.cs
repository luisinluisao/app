using Slyth.Core.Backup;
using Slyth.Core.Tweaks;

namespace Slyth.Core.Tests;

public sealed class CleanupTweakTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "slyth-" + Guid.NewGuid())).FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string relative, int size, int ageDays)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-ageDays));
        return path;
    }

    [Fact]
    public void Deletes_only_old_files_removes_empty_folders_and_counts_freed_space()
    {
        var old = Write(Path.Combine("old-folder", "old.tmp"), 2048, 3);
        var recent = Write("new.tmp", 10, 0);
        var context = new TweakContext(new FakeRegistry(), new FakeCommands(), new BackupSession());
        var tweak = new CleanupTweak { Id = "c", Name = "c", Description = "", Paths = [_dir] };

        var detail = tweak.Apply(context);

        Assert.False(File.Exists(old));
        Assert.False(Directory.Exists(Path.Combine(_dir, "old-folder")));
        Assert.True(File.Exists(recent));
        Assert.True(Directory.Exists(_dir));
        Assert.Equal("1 arquivos, 2 KB liberados", detail);
        Assert.Equal(2048, context.FreedBytes);
    }

    [Fact]
    public void Wildcards_match_every_profile_and_single_files_can_be_targeted()
    {
        var a = Write(Path.Combine("User Data", "Default", "Cache", "f1"), 1, 3);
        var b = Write(Path.Combine("User Data", "Profile 2", "Cache", "f2"), 1, 3);
        var keep = Write(Path.Combine("User Data", "Default", "Login Data"), 1, 3);
        var dump = Write("MEMORY.DMP", 1, 3);
        var tweak = new CleanupTweak
        {
            Id = "c", Name = "c", Description = "",
            Paths = [Path.Combine(_dir, "User Data", "*", "Cache"), Path.Combine(_dir, "MEMORY.DMP")],
        };

        tweak.Apply(new TweakContext(new FakeRegistry(), new FakeCommands(), new BackupSession()));

        Assert.False(File.Exists(a));
        Assert.False(File.Exists(b));
        Assert.False(File.Exists(dump));
        Assert.True(File.Exists(keep));
    }

    [Fact]
    public void Unknown_environment_variables_resolve_to_nothing() =>
        Assert.Empty(CleanupTweak.Resolve("%SLYTH_DOES_NOT_EXIST%\\Cache"));
}
