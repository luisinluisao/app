namespace Slyth.Core.Tweaks;

/// <summary>
/// Desativa tarefas agendadas do Windows (schtasks). Tarefas que não existem neste PC são ignoradas;
/// cada tarefa desativada guarda o comando que a reativa.
/// </summary>
public sealed class ScheduledTaskTweak : ITweak
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public TweakCategory Category { get; init; } = TweakCategory.Privacy;

    public TweakLevel Level { get; init; } = TweakLevel.Manual;

    public bool RequiresRestart => false;

    public bool Reversible => true;

    public required IReadOnlyList<string> Tasks { get; init; }

    public bool IsApplied(TweakContext context) => Tasks.All(t => IsEnabled(context, t) != true);

    public string? Apply(TweakContext context)
    {
        var disabled = 0;
        var denied = 0;
        foreach (var task in Tasks.Where(t => IsEnabled(context, t) == true))
        {
            if (context.Commands.Run("schtasks", $"/Change /TN \"{task}\" /Disable").Success)
            {
                context.RecordCommand("schtasks", $"/Change /TN \"{task}\" /Enable");
                disabled++;
            }
            else
            {
                denied++;
            }
        }

        if (disabled == 0 && denied > 0)
        {
            throw new InvalidOperationException("O Windows não permitiu alterar essas tarefas.");
        }

        return denied == 0 ? $"{disabled} tarefa(s) desativada(s)" : $"{disabled} tarefa(s) desativada(s), {denied} protegida(s) pelo Windows";
    }

    /// <summary>null = tarefa não existe. O XML não é traduzido, ao contrário da saída normal do schtasks.</summary>
    internal static bool? IsEnabled(TweakContext context, string task)
    {
        var result = context.Commands.Run("schtasks", $"/Query /TN \"{task}\" /XML");
        if (!result.Success)
        {
            return null;
        }

        return !result.Output.Contains("<Enabled>false</Enabled>", StringComparison.OrdinalIgnoreCase);
    }
}
