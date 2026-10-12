using System.Net;

namespace SlideProRelay.Server.Security;

/// <summary>
/// Keeps the operator-only surface (settings page and API, diagnostics) off the
/// LAN. The relay listens on every interface so phones can reach the slide view,
/// but the settings endpoints expose the SlidePro API key and can restart the
/// relay, so they are served only to callers on this machine. Everyone else gets
/// a bare 404 for every HTTP method, so the routes never advertise themselves.
/// </summary>
public static class LoopbackOnly
{
    /// <summary>
    /// True for a loopback address: 127.0.0.0/8, ::1, or the IPv4-mapped form
    /// (::ffff:127.0.0.1) that Kestrel reports for IPv4 clients of a dual-stack
    /// listener. Null (no transport address) counts as remote.
    /// </summary>
    public static bool IsLoopback(IPAddress? address)
    {
        if (address is null) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return IPAddress.IsLoopback(address);
    }

    /// <summary>
    /// True when a CORS <c>Origin</c> value names this machine: http or https,
    /// host <c>localhost</c>, a 127.x.x.x address or [::1], on any port.
    /// </summary>
    public static bool IsLoopbackOrigin(string? origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;

        return uri.HostNameType switch
        {
            UriHostNameType.Dns => string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase),
            UriHostNameType.IPv4 or UriHostNameType.IPv6 =>
                IPAddress.TryParse(uri.IdnHost, out var ip) && IsLoopback(ip),
            _ => false,
        };
    }

    /// <summary>
    /// Ends requests for any of <paramref name="paths"/> with an empty 404 unless
    /// they come from a loopback address. Add it before routing: that way no
    /// endpoint code (not even model binding) runs for a remote caller, and a
    /// method mismatch cannot leak the route through a 405 with an Allow header.
    /// </summary>
    public static IApplicationBuilder UseLoopbackOnly(this IApplicationBuilder app, IEnumerable<string> paths)
    {
        var restricted = paths.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return app.Use((ctx, next) =>
        {
            if (restricted.Contains(Normalize(ctx.Request.Path.Value))
                && !IsLoopback(ctx.Connection.RemoteIpAddress))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            return next(ctx);
        });
    }

    // Routing ignores case and a trailing slash, so the comparison must too.
    private static string Normalize(string? path) =>
        string.IsNullOrEmpty(path) ? "/" : path.Length > 1 ? path.TrimEnd('/') : path;
}
