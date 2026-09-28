using Slyth.Core.Backup;
using Slyth.Core.Platform;

namespace Slyth.Core.Tests;

internal sealed class FakeRegistry : IRegistry
{
    public Dictionary<(RegistryRoot, string, string), RegistryValue> Values { get; } = [];

    public RegistryValue? GetValue(RegistryRoot root, string key, string name) =>
        Values.TryGetValue((root, key.ToLowerInvariant(), name.ToLowerInvariant()), out var v) ? v : null;

    public void SetValue(RegistryRoot root, string key, string name, RegistryValue value) =>
        Values[(root, key.ToLowerInvariant(), name.ToLowerInvariant())] = value;

    public void DeleteValue(RegistryRoot root, string key, string name) =>
        Values.Remove((root, key.ToLowerInvariant(), name.ToLowerInvariant()));

    public IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string key)
    {
        var prefix = key.ToLowerInvariant() + "\\";
        return Values.Keys
            .Where(k => k.Item1 == root && k.Item2.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => k.Item2[prefix.Length..].Split('\\')[0])
            .Distinct()
            .ToList();
    }
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
