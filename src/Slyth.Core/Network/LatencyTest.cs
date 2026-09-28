using System.Net.NetworkInformation;

namespace Slyth.Core.Network;

public sealed record LatencyResult(double AverageMs, double MinMs, double MaxMs, double JitterMs, double LossPercent, int Sent)
{
    /// <summary>Resumo em linguagem simples para quem joga online.</summary>
    public string Verdict => LossPercent switch
    {
        > 2 => "Perda de pacotes: espere teleportes e travadas online",
        _ when AverageMs <= 30 && JitterMs <= 5 => "Excelente para jogos competitivos",
        _ when AverageMs <= 60 && JitterMs <= 10 => "Boa para jogos online",
        _ when JitterMs > 15 => "Instável: o ping varia muito (Wi-Fi ou rede congestionada)",
        _ => "Ping alto: prefira cabo e servidores mais próximos",
    };

    public static LatencyResult From(IReadOnlyList<double?> samples)
    {
        var ok = samples.Where(s => s.HasValue).Select(s => s!.Value).ToList();
        if (ok.Count == 0)
        {
            return new LatencyResult(0, 0, 0, 0, 100, samples.Count);
        }

        var jitter = ok.Count < 2 ? 0 : ok.Zip(ok.Skip(1), (a, b) => Math.Abs(b - a)).Average();
        return new LatencyResult(ok.Average(), ok.Min(), ok.Max(), jitter, 100.0 * (samples.Count - ok.Count) / samples.Count, samples.Count);
    }
}

/// <summary>Mede ping, jitter (variação) e perda de pacotes com ICMP.</summary>
public static class LatencyTest
{
    public static async Task<LatencyResult> RunAsync(string host, int count, IProgress<double?>? sample = null, CancellationToken cancel = default)
    {
        using var ping = new Ping();
        var samples = new List<double?>(count);
        for (var i = 0; i < count; i++)
        {
            cancel.ThrowIfCancellationRequested();
            double? rtt = null;
            try
            {
                var reply = await ping.SendPingAsync(host, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    rtt = reply.RoundtripTime;
                }
            }
            catch (PingException)
            {
            }

            samples.Add(rtt);
            sample?.Report(rtt);
            await Task.Delay(120, cancel);
        }

        return LatencyResult.From(samples);
    }
}
