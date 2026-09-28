namespace Slyth.Core.Tweaks;

/// <summary>Ajuste feito por um comando do sistema, opcionalmente com um comando que o desfaz.</summary>
public sealed class CommandTweak : ITweak
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required TweakCategory Category { get; init; }

    public TweakLevel Level { get; init; } = TweakLevel.Manual;

    public bool RequiresRestart { get; init; }

    public bool Reversible => Revert is not null;

    public required (string File, string Args) Command { get; init; }

    public (string File, string Args)? Revert { get; init; }

    /// <summary>Como saber se já está aplicado; sem isso o comando sempre roda.</summary>
    public Func<TweakContext, bool>? AppliedCheck { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    public string? SuccessDetail { get; init; }

    public bool IsApplied(TweakContext context) => AppliedCheck?.Invoke(context) ?? false;

    public string? Apply(TweakContext context)
    {
        var result = context.Commands.Run(Command.File, Command.Args, Timeout);
        if (!result.Success)
        {
            var message = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            throw new InvalidOperationException(message.Trim() is { Length: > 0 } m ? m : $"{Command.File} retornou {result.ExitCode}");
        }

        if (Revert is { } revert)
        {
            context.RecordCommand(revert.File, revert.Args);
        }

        return SuccessDetail;
    }
}
