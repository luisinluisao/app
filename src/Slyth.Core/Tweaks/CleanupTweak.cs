namespace Slyth.Core.Tweaks;

/// <summary>
/// Apaga arquivos antigos em pastas de cache/temporários. Arquivos em uso ou recentes são preservados.
/// Caminhos aceitam variáveis de ambiente (%LOCALAPPDATA%) e '*' em qualquer trecho (ex.: perfis do navegador).
/// Um caminho que aponta para um arquivo apaga só aquele arquivo.
/// </summary>
public sealed class CleanupTweak : ITweak
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public TweakCategory Category => TweakCategory.Cleanup;

    public TweakLevel Level { get; init; } = TweakLevel.Safe;

    public bool RequiresRestart => false;

    public bool Reversible => false;

    public required IReadOnlyList<string> Paths { get; init; }

    public TimeSpan MinimumAge { get; init; } = TimeSpan.FromDays(1);

    public bool IsApplied(TweakContext context) => false;

    public string? Apply(TweakContext context)
    {
        var cutoff = DateTime.UtcNow - MinimumAge;
        long freed = 0;
        var deleted = 0;

        foreach (var path in Paths.SelectMany(Resolve).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(path))
            {
                TryDelete(new FileInfo(path), cutoff, ref freed, ref deleted);
                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*", Recursive))
            {
                TryDelete(new FileInfo(file), cutoff, ref freed, ref deleted);
            }

            RemoveEmptyDirectories(path);
        }

        context.FreedBytes += freed;
        return deleted == 0 ? "nada para limpar" : $"{deleted} arquivos, {Bytes.Format(freed)} liberados";
    }

    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    /// <summary>Expande variáveis de ambiente e curingas em uma lista de caminhos existentes.</summary>
    internal static IEnumerable<string> Resolve(string pattern)
    {
        var expanded = Environment.ExpandEnvironmentVariables(pattern);
        if (expanded.Contains('%'))
        {
            yield break; // variável inexistente neste PC
        }

        var root = Path.GetPathRoot(expanded);
        if (string.IsNullOrEmpty(root))
        {
            yield break;
        }

        var segments = expanded[root.Length..].Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<string> current = [root];
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var last = i == segments.Length - 1;
            current = segment.Contains('*')
                ? current.Where(Directory.Exists).SelectMany(dir => Matches(dir, segment, includeFiles: last)).ToList()
                : current.Select(dir => Path.Combine(dir, segment)).ToList();
        }

        foreach (var path in current)
        {
            yield return path;
        }
    }

    private static IEnumerable<string> Matches(string dir, string segment, bool includeFiles)
    {
        try
        {
            var dirs = Directory.GetDirectories(dir, segment);
            return includeFiles ? dirs.Concat(Directory.GetFiles(dir, segment)) : dirs;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void TryDelete(FileInfo info, DateTime cutoff, ref long freed, ref int deleted)
    {
        try
        {
            if (info.LastWriteTimeUtc > cutoff)
            {
                return;
            }

            var size = info.Length;
            info.Delete();
            freed += size;
            deleted++;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Em uso ou protegido: deixa como está.
        }
    }

    private static void RemoveEmptyDirectories(string root)
    {
        foreach (var dir in Directory.EnumerateDirectories(root, "*", Recursive).OrderByDescending(d => d.Length).ToList())
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
