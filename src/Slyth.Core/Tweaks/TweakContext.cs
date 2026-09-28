using Slyth.Core.Backup;
using Slyth.Core.Platform;

namespace Slyth.Core.Tweaks;

/// <summary>
/// Tudo o que um ajuste pode usar. Toda escrita passa por aqui para que o valor
/// anterior seja registrado no backup antes de ser alterado.
/// </summary>
public sealed class TweakContext(IRegistry registry, ICommandRunner commands, BackupSession session)
{
    public IRegistry Registry { get; } = registry;

    public ICommandRunner Commands { get; } = commands;

    public BackupSession Session { get; } = session;

    internal string CurrentTweakId { get; set; } = "";

    /// <summary>Espaço liberado pelas limpezas desta execução.</summary>
    public long FreedBytes { get; set; }

    public void SetRegistry(RegistryRoot root, string key, string name, RegistryValue value)
    {
        var alreadyTracked = Session.Changes.Any(c =>
            c.Kind == ChangeKind.Registry && c.Root == root &&
            string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        if (!alreadyTracked)
        {
            Session.Changes.Add(new ChangeRecord
            {
                Kind = ChangeKind.Registry,
                TweakId = CurrentTweakId,
                Root = root,
                Key = key,
                Name = name,
                PreviousValue = Registry.GetValue(root, key, name),
            });
        }

        Registry.SetValue(root, key, name, value);
    }

    public void RecordCommand(string revertFile, string revertArgs) => Session.Changes.Add(new ChangeRecord
    {
        Kind = ChangeKind.Command,
        TweakId = CurrentTweakId,
        RevertFile = revertFile,
        RevertArgs = revertArgs,
    });

    public void RecordPowerScheme(string previousScheme) => Session.Changes.Add(new ChangeRecord
    {
        Kind = ChangeKind.PowerScheme,
        TweakId = CurrentTweakId,
        PreviousPowerScheme = previousScheme,
    });
}
