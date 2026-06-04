using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ProSlideRelay.Server.Startup;

public static class NetworkUrlPrinter
{
    public static void Print(ILogger logger, IEnumerable<string> addresses)
    {
        var lanIps = GetLanAddresses();

        logger.LogInformation("─────────────────────────────────────────");
        logger.LogInformation("  ProSlideRelay is running");
        logger.LogInformation(" ");

        foreach (var addr in addresses)
            logger.LogInformation("  Local:   {Address}", addr);

        foreach (var ip in lanIps)
        {
            // Rewrite localhost/0.0.0.0 addresses with the LAN IP for phone use
            foreach (var addr in addresses)
            {
                var uri = new Uri(addr);
                if (uri.Host is "localhost" or "0.0.0.0" or "[::]")
                    logger.LogInformation("  Network: {Scheme}://{Ip}:{Port}", uri.Scheme, ip, uri.Port);
            }
        }

        logger.LogInformation(" ");
        logger.LogInformation("  Open the Network URL on your phone to follow along.");
        logger.LogInformation("─────────────────────────────────────────");
    }

    public static string? GetLanIp() => GetLanAddresses().FirstOrDefault();

    private static IReadOnlyList<string> GetLanAddresses()
    {
        var results = new List<string>();

        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.OperationalStatus != OperationalStatus.Up) continue;
            if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

            foreach (var addr in iface.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    results.Add(addr.Address.ToString());
            }
        }

        return results;
    }
}
