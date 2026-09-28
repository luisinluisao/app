using PcOptimizer.Core.Backup;
using PcOptimizer.Core.Platform;
using PcOptimizer.Core.Tweaks;

namespace PcOptimizer.Core.Optimization;

/// <summary>
/// Executa o "um clique": ponto de restauração, backup de cada valor alterado,
/// aplicação dos ajustes e relatório. Um ajuste que falha é desfeito sozinho e não impede os outros.
/// </summary>
public sealed class Optimizer(
    IRegistry registry,
    ICommandRunner commands,
    IBackupStore backups,
    IRestorePointService? restorePoints = null)
{
    public OptimizationReport Run(IEnumerable<ITweak> tweaks, IProgress<string>? progress = null)
    {
        var list = tweaks.ToList();
        var restorePointCreated = false;
        if (restorePoints is not null && list.Any(t => t.Reversible))
        {
            progress?.Report("Criando ponto de restauração do Windows...");
            (restorePointCreated, var message) = restorePoints.TryCreate("PC Optimizer - antes da otimização");
            progress?.Report(message);
        }

        var session = new BackupSession();
        var context = new TweakContext(registry, commands, session);
        var results = new List<TweakResult>();
        var restart = false;

        foreach (var tweak in list)
        {
            progress?.Report($"{tweak.Name}...");
            context.CurrentTweakId = tweak.Id;
            var changesBefore = session.Changes.Count;
            try
            {
                if (tweak.IsApplied(context))
                {
                    results.Add(new TweakResult(tweak.Id, tweak.Name, TweakStatus.AlreadyApplied, null));
                    continue;
                }

                var detail = tweak.Apply(context);
                session.AppliedTweaks.Add(tweak.Id);
                restart |= tweak.RequiresRestart;
                results.Add(new TweakResult(tweak.Id, tweak.Name, TweakStatus.Applied, detail));
            }
            catch (Exception e)
            {
                var partial = session.Changes.Skip(changesBefore).ToList();
                session.Changes.RemoveRange(changesBefore, partial.Count);
                RevertChanges(partial, []);
                results.Add(new TweakResult(tweak.Id, tweak.Name, TweakStatus.Failed, e.Message));
            }

            // Salva a cada passo: se o PC desligar no meio, o que já mudou pode ser desfeito.
            if (session.Changes.Count > 0)
            {
                backups.Save(session);
            }
        }

        progress?.Report("Concluído.");
        return new OptimizationReport(session.Changes.Count > 0 ? session.Id : null, restorePointCreated, results, restart);
    }

    public RevertReport Revert(BackupSession session)
    {
        var errors = new List<string>();
        var restored = RevertChanges(session.Changes, errors);
        session.RevertedAt = DateTime.Now;
        backups.Save(session);
        return new RevertReport(restored, errors);
    }

    private int RevertChanges(IEnumerable<ChangeRecord> changes, List<string> errors)
    {
        var restored = 0;
        foreach (var change in changes.Reverse())
        {
            try
            {
                switch (change.Kind)
                {
                    case ChangeKind.Registry when change.PreviousValue is null:
                        registry.DeleteValue(change.Root!.Value, change.Key!, change.Name!);
                        break;
                    case ChangeKind.Registry:
                        registry.SetValue(change.Root!.Value, change.Key!, change.Name!, change.PreviousValue);
                        break;
                    case ChangeKind.PowerScheme:
                        var result = commands.Run("powercfg", $"/setactive {change.PreviousPowerScheme}");
                        if (!result.Success)
                        {
                            throw new InvalidOperationException(result.Error.Trim());
                        }

                        break;
                }

                restored++;
            }
            catch (Exception e)
            {
                errors.Add($"{change.TweakId}: {e.Message}");
            }
        }

        return restored;
    }
}
