using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Pdv.Server;

/// <summary>Descobre em que endereço da rede local o celular deve se conectar.</summary>
public static class RedeLocal
{
    /// <summary>Faixas privadas de rede doméstica/comercial: 10.x, 172.16–31.x e 192.168.x.</summary>
    public static bool EhEnderecoPrivado(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;

        var b = ip.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] is >= 16 and <= 31)
               || (b[0] == 192 && b[1] == 168);
    }

    /// <summary>
    /// Endereços IPv4 deste computador, do mais provável para o menos provável de ser o Wi-Fi/cabo real:
    /// interfaces com gateway primeiro (redes virtuais de Docker/VM costumam não ter), depois faixas privadas.
    /// </summary>
    public static IReadOnlyList<IPAddress> EnderecosIPv4()
    {
        try
        {
            var candidatos = new List<(IPAddress Ip, bool TemGateway)>();

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                var props = nic.GetIPProperties();
                var temGateway = props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));

                foreach (var unicast in props.UnicastAddresses)
                {
                    var ip = unicast.Address;
                    if (ip.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (ip.GetAddressBytes() is [169, 254, ..]) continue; // endereço automático sem rede

                    candidatos.Add((ip, temGateway));
                }
            }

            return candidatos
                .OrderByDescending(c => c.TemGateway)
                .ThenByDescending(c => EhEnderecoPrivado(c.Ip))
                .Select(c => c.Ip)
                .Distinct()
                .ToList();
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }
}
