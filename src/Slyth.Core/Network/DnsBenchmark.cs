using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Slyth.Core.Network;

public sealed record DnsProvider(string Name, string Primary, string Secondary)
{
    public string NameServer => $"{Primary},{Secondary}";

    public static readonly IReadOnlyList<DnsProvider> Known =
    [
        new("Cloudflare", "1.1.1.1", "1.0.0.1"),
        new("Google", "8.8.8.8", "8.8.4.4"),
        new("Quad9", "9.9.9.9", "149.112.112.112"),
        new("OpenDNS", "208.67.222.222", "208.67.220.220"),
    ];
}

/// <summary>Mede quanto tempo cada servidor DNS leva para responder, com consultas UDP reais.</summary>
public static class DnsBenchmark
{
    /// <summary>Sites que gamers acessam; ficam no cache dos servidores, então medimos a resposta da rede até o DNS.</summary>
    public static readonly IReadOnlyList<string> Hosts =
        ["google.com", "youtube.com", "discord.com", "steamcommunity.com", "riotgames.com", "epicgames.com", "twitch.tv", "microsoft.com"];

    /// <summary>Mediana em ms das respostas, ou null se o servidor não respondeu.</summary>
    public static async Task<double?> MeasureAsync(IPAddress server, CancellationToken cancel = default)
    {
        using var udp = new UdpClient(server.AddressFamily);
        udp.Connect(server, 53);
        var times = new List<double>();
        ushort id = (ushort)Random.Shared.Next(1, ushort.MaxValue);

        foreach (var host in Hosts)
        {
            id++;
            var query = BuildQuery(id, host);
            var watch = Stopwatch.StartNew();
            try
            {
                await udp.SendAsync(query, cancel);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                timeout.CancelAfter(1500);
                while (true)
                {
                    var response = await udp.ReceiveAsync(timeout.Token);
                    if (IsResponseTo(response.Buffer, id))
                    {
                        times.Add(watch.Elapsed.TotalMilliseconds);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
            {
                // sem resposta dentro do limite
            }
            catch (SocketException)
            {
            }
        }

        if (times.Count == 0)
        {
            return null;
        }

        times.Sort();
        return times[times.Count / 2];
    }

    /// <summary>Consulta DNS padrão (RFC 1035) do tipo A com recursão.</summary>
    public static byte[] BuildQuery(ushort id, string host)
    {
        var buffer = new List<byte>(32 + host.Length);
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header, id);
        BinaryPrimitives.WriteUInt16BigEndian(header[2..], 0x0100); // RD
        BinaryPrimitives.WriteUInt16BigEndian(header[4..], 1); // QDCOUNT
        buffer.AddRange(header.ToArray());

        foreach (var label in host.TrimEnd('.').Split('.'))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            buffer.Add((byte)bytes.Length);
            buffer.AddRange(bytes);
        }

        buffer.AddRange([0, 0, 1, 0, 1]); // fim do nome, QTYPE=A, QCLASS=IN
        return buffer.ToArray();
    }

    public static bool IsResponseTo(byte[] packet, ushort id) =>
        packet.Length >= 12 &&
        BinaryPrimitives.ReadUInt16BigEndian(packet) == id &&
        (packet[2] & 0x80) != 0;
}
