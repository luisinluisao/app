namespace PcOptimizer.Core.Tweaks;

/// <summary>Apaga arquivos temporários antigos. Arquivos em uso ou recentes são preservados.</summary>
public sealed class TempCleanupTweak(IReadOnlyList<string> directories, TimeSpan minimumAge) : ITweak
{
    public TempCleanupTweak()
        : this(DefaultDirectories(), TimeSpan.FromDays(1))
    {
    }

    public string Id => "temp-cleanup";

    public string Name => "Limpar arquivos temporários";

    public string Description => "Remove arquivos temporários com mais de 1 dia do Windows e do usuário. " +
        "Arquivos em uso são ignorados. Não pode ser desfeito (e não precisa).";

    public TweakCategory Category => TweakCategory.Cleanup;

    public bool Recommended => true;

    public bool RequiresRestart => false;

    public bool Reversible => false;

    public bool IsApplied(TweakContext context) => false;

    public string? Apply(TweakContext context)
    {
        var cutoff = DateTime.UtcNow - minimumAge;
        long freed = 0;
        var deleted = 0;

        foreach (var dir in directories.Where(Directory.Exists))
        {
            foreach (var file in SafeEnumerate(dir))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTimeUtc > cutoff)
                    {
                        continue;
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

            RemoveEmptyDirectories(dir);
        }

        return $"{deleted} arquivos removidos, {FormatBytes(freed)} liberados";
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):0} KB",
        _ => $"{bytes} bytes",
    };

    private static IReadOnlyList<string> DefaultDirectories()
    {
        var windir = Environment.GetEnvironmentVariable("WINDIR");
        return windir is null ? [Path.GetTempPath()] : [Path.GetTempPath(), Path.Combine(windir, "Temp")];
    }

    private static IEnumerable<string> SafeEnumerate(string dir) =>
        Directory.EnumerateFiles(dir, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        });

    private static void RemoveEmptyDirectories(string root)
    {
        var subdirs = Directory.EnumerateDirectories(root, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        }).OrderByDescending(d => d.Length);

        foreach (var dir in subdirs)
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
