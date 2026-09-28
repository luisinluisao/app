using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Slyth.App.Services;

/// <summary>O adaptador que está de fato levando à internet (tem gateway IPv4).</summary>
public static class NetworkInfo
{
    public static (NetworkInterface Nic, IPInterfaceProperties Props)? ActiveInterface()
    {
        var active = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(n => (Nic: n, Props: n.GetIPProperties()))
            .FirstOrDefault(x => x.Props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork));
        return active.Nic is null ? null : active;
    }

    public static IReadOnlyList<IPAddress> CurrentDns() =>
        ActiveInterface()?.Props.DnsAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToList() ?? [];
}
