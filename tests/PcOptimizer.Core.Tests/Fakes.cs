using PcOptimizer.Core.Backup;
using PcOptimizer.Core.Platform;

namespace PcOptimizer.Core.Tests;

internal sealed class FakeRegistry : IRegistry
{
    public Dictionary<(RegistryRoot, string, string), RegistryValue> Values { get; } = [];

    public RegistryValue? GetValue(RegistryRoot root, string key, string name) =>
        Values.TryGetValue((root, key.ToLowerInvariant(), name.ToLowerInvariant()), out var v) ? v : null;

    public void SetValue(RegistryRoot root, string key, string name, RegistryValue value) =>
        Values[(root, key.ToLowerInvariant(), name.ToLowerInvariant())] = value;

    public void DeleteValue(RegistryRoot root, string key, string name) =>
        Values.Remove((root, key.ToLowerInvariant(), name.ToLowerInvariant()));
}

internal sealed class FakeCommands : ICommandRunner
{
    public List<string> Calls { get; } = [];

    public Func<string, CommandResult> Handler { get; set; } = _ => new CommandResult(0, "", "");

    public CommandResult Run(string fileName, string arguments, TimeSpan? timeout = null)
    {
        var line = $"{fileName} {arguments}";
        Calls.Add(line);
        return Handler(line);
    }
}

internal sealed class MemoryBackupStore : IBackupStore
{
    public Dictionary<string, BackupSession> Saved { get; } = [];

    public void Save(BackupSession session) => Saved[session.Id] = session;

    public IReadOnlyList<BackupSession> LoadAll() => Saved.Values.ToList();
}
