using Slyth.Core.Backup;
using Slyth.Core.Platform;

namespace Slyth.Core.Tests;

internal sealed class FakeRegistry : IRegistry
{
    /// <summary>Como no Windows: chaves e nomes sem diferenciar maiúsculas, mas preservando a grafia original.</summary>
    public Dictionary<(RegistryRoot Root, string Key, string Name), RegistryValue> Values { get; } = new(new KeyComparer());

    public RegistryValue? GetValue(RegistryRoot root, string key, string name) =>
        Values.TryGetValue((root, key, name), out var v) ? v : null;

    public void SetValue(RegistryRoot root, string key, string name, RegistryValue value)
    {
        Values.Remove((root, key, name));
        Values[(root, key, name)] = value;
    }

    public void DeleteValue(RegistryRoot root, string key, string name) => Values.Remove((root, key, name));

    public IReadOnlyList<string> GetValueNames(RegistryRoot root, string key) =>
        Values.Keys.Where(k => k.Root == root && string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase)).Select(k => k.Name).ToList();

    public IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string key)
    {
        var prefix = key + "\\";
        return Values.Keys
            .Where(k => k.Root == root && k.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => k.Key[prefix.Length..].Split('\\')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class KeyComparer : IEqualityComparer<(RegistryRoot Root, string Key, string Name)>
    {
        public bool Equals((RegistryRoot Root, string Key, string Name) x, (RegistryRoot Root, string Key, string Name) y) =>
            x.Root == y.Root &&
            string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((RegistryRoot Root, string Key, string Name) k) =>
            HashCode.Combine(k.Root, k.Key.ToUpperInvariant(), k.Name.ToUpperInvariant());
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
