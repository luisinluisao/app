using System.Text.Json;

namespace Slyth.Core.Performance;

/// <summary>Guarda as medições como JSON (por padrão em %ProgramData%\SlythOptimizer\Benchmarks).</summary>
public sealed class SnapshotStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SlythOptimizer", "Benchmarks");

    public void Save(PerformanceSnapshot snapshot)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{snapshot.TakenAt:yyyyMMdd-HHmmss-fff}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, Options));
    }

    /// <summary>Da mais antiga para a mais recente.</summary>
    public IReadOnlyList<PerformanceSnapshot> LoadAll()
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var list = new List<PerformanceSnapshot>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<PerformanceSnapshot>(File.ReadAllText(file), Options) is { } s)
                {
                    list.Add(s);
                }
            }
            catch (JsonException)
            {
            }
        }

        return list.OrderBy(s => s.TakenAt).ToList();
    }

    /// <summary>Apaga tudo menos a última medição, que vira a nova base de comparação.</summary>
    public void ResetBaseline()
    {
        var all = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json").Order().ToList() : [];
        foreach (var file in all.Take(Math.Max(0, all.Count - 1)))
        {
            File.Delete(file);
        }
    }
}
