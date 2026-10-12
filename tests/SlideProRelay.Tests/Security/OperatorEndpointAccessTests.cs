using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SlideProRelay.Server;

namespace SlideProRelay.Tests.Security;

/// <summary>
/// The relay binds every interface for the phones, so the operator-only routes
/// must be invisible to anything that is not this machine, while the phone
/// routes stay open. These run the real pipeline on the in-memory TestServer.
/// </summary>
public sealed class OperatorEndpointAccessTests(RelayTestHost host) : IClassFixture<RelayTestHost>
{
    private static readonly IPAddress Lan = IPAddress.Parse("192.168.1.20");
    private static readonly IPAddress Loopback = IPAddress.Loopback;

    // Everything phones use. /events is covered on its own because it streams.
    private static readonly string[] PublicPaths =
    [
        "/",
        "/api/current",
        "/api/health",
        "/api/qr",
        "/api/slide-image",
        "/api/screen-capture",
        "/api/screen-capture/key",
    ];

    private static readonly string[] LanAddresses =
        ["192.168.1.20", "10.0.0.5", "fe80::1", "::ffff:192.168.1.20"];

    private static readonly string[] LoopbackAddresses =
        ["127.0.0.1", "::1", "::ffff:127.0.0.1"];

    public static TheoryData<string> OperatorPaths => ToData(ServerHost.OperatorPaths);
    public static TheoryData<string> PublicPathData => ToData(PublicPaths);
    public static TheoryData<string, string> OperatorPathsFromLan => Cross(ServerHost.OperatorPaths, LanAddresses);
    public static TheoryData<string, string> OperatorPathsFromLoopback => Cross(ServerHost.OperatorPaths, LoopbackAddresses);

    // ── Operator routes: refused off-machine ──────────────────────────────────

    [Theory, MemberData(nameof(OperatorPathsFromLan))]
    public async Task OperatorRoute_FromLan_IsAnEmpty404(string path, string address)
    {
        var ctx = await host.SendAsync(path, IPAddress.Parse(address));

        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.Equal("", await RelayTestHost.ReadBodyAsync(ctx));
        Assert.Null(ctx.GetEndpoint()); // refused before routing: no endpoint code ran
    }

    [Theory, MemberData(nameof(OperatorPaths))]
    public async Task OperatorRoute_FromLan_Is404ForEveryMethod(string path)
    {
        // A 405 with an Allow header would reveal the route just as well as a 200.
        foreach (var method in new[] { "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD" })
        {
            var ctx = await host.SendAsync(path, Lan, method);

            Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
            Assert.False(ctx.Response.Headers.ContainsKey("Allow"), $"{method} {path} leaked an Allow header");
        }
    }

    [Theory, MemberData(nameof(OperatorPaths))]
    public async Task OperatorRoute_FromLan_IsHiddenForRoutingEquivalentPaths(string path)
    {
        // Routing ignores case and a trailing slash, so these reach the same
        // endpoint from loopback; the filter must treat them the same way.
        foreach (var variant in new[] { path.ToUpperInvariant(), path + "/", path.ToUpperInvariant() + "/" })
        {
            var fromLan = await host.SendAsync(variant, Lan);
            Assert.Equal(StatusCodes.Status404NotFound, fromLan.Response.StatusCode);

            var fromLoopback = await host.SendAsync(variant, Loopback);
            Assert.NotEqual(StatusCodes.Status404NotFound, fromLoopback.Response.StatusCode);
        }

        // Not a routing match at all, but it must not slip past the filter either.
        var doubleSlash = await host.SendAsync("/" + path, Lan);
        Assert.Equal(StatusCodes.Status404NotFound, doubleSlash.Response.StatusCode);
    }

    [Theory, MemberData(nameof(OperatorPaths))]
    public async Task OperatorRoute_WithoutTransportAddress_Is404(string path)
    {
        var ctx = await host.SendAsync(path, remote: null);

        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
    }

    // ── Operator routes: served on-machine ────────────────────────────────────

    [Theory, MemberData(nameof(OperatorPathsFromLoopback))]
    public async Task OperatorRoute_FromLoopback_IsServed(string path, string address)
    {
        var ctx = await host.SendAsync(path, IPAddress.Parse(address));

        Assert.NotEqual(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.NotNull(ctx.GetEndpoint());
    }

    [Fact]
    public async Task Settings_FromLoopback_ReturnsTheApiKey()
    {
        var ctx = await host.SendAsync("/api/settings", Loopback);

        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.Contains(RelayTestHost.SeedApiKey, await RelayTestHost.ReadBodyAsync(ctx));
    }

    [Fact]
    public async Task SettingsPost_FromLan_IsRefusedAndSavesNothing()
    {
        var before = await File.ReadAllTextAsync(host.SettingsPath);

        var ctx = await host.SendAsync("/api/settings", Lan, "POST", SettingsBody(pollingIntervalMs: 999));

        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.Equal(before, await File.ReadAllTextAsync(host.SettingsPath));
    }

    [Fact]
    public async Task SettingsPost_FromLoopback_Saves()
    {
        var ctx = await host.SendAsync("/api/settings", Loopback, "POST", SettingsBody(pollingIntervalMs: 750));

        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.Contains("\"PollingIntervalMs\": 750", await File.ReadAllTextAsync(host.SettingsPath));
    }

    // ── Phone routes: open to the LAN ─────────────────────────────────────────

    [Theory, MemberData(nameof(PublicPathData))]
    public async Task PublicRoute_FromLan_IsServed(string path)
    {
        var ctx = await host.SendAsync(path, Lan);

        Assert.NotEqual(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.NotNull(ctx.GetEndpoint());
    }

    [Fact]
    public async Task EventStream_FromLan_IsServed()
    {
        using var hangUp = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Completes once the response has started, i.e. after the first event.
        var ctx = await host.SendAsync("/events", Lan, ct: hangUp.Token);

        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.Equal("text/event-stream", ctx.Response.ContentType);

        hangUp.Cancel();
    }

    // ── CORS: only the health poll of the settings page, only from localhost ──

    [Fact]
    public async Task HealthPoll_FromLocalhostOrigin_GetsCorsHeader()
    {
        // The settings page, still open on http://localhost:<old port>, polling the new port.
        var ctx = await host.SendAsync("/api/health", Loopback, origin: "http://localhost:5173");

        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.Equal("http://localhost:5173", ctx.Response.Headers.AccessControlAllowOrigin.ToString());
    }

    [Fact]
    public async Task HealthPoll_FromLanOrigin_GetsNoCorsHeader()
    {
        var ctx = await host.SendAsync("/api/health", Lan, origin: "http://192.168.1.20:5174");

        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode); // still open to the LAN...
        Assert.False(ctx.Response.Headers.ContainsKey("Access-Control-Allow-Origin")); // ...but never cross-origin
    }

    [Theory]
    [InlineData("/api/current")]
    [InlineData("/settings")]
    public async Task OtherRoutes_NeverGetCorsHeaders(string path)
    {
        var ctx = await host.SendAsync(path, Loopback, origin: "http://localhost:5173");

        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.False(ctx.Response.Headers.ContainsKey("Access-Control-Allow-Origin"));
    }

    // ── Route table: every route has been classified ──────────────────────────

    [Fact]
    public void EveryRoute_IsEitherOperatorOnlyOrDeliberatelyPublic()
    {
        var routes = ((IEndpointRouteBuilder)host.App).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.NotEmpty(routes);

        var known = ServerHost.OperatorPaths.Concat(PublicPaths).Append("/events");
        var unclassified = routes.Except(known, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(unclassified.Count == 0,
            "Decide whether these routes are operator-only or public, then add them to " +
            $"ServerHost.OperatorPaths or to PublicPaths in this test: {string.Join(", ", unclassified)}");

        // ...and nothing listed as operator-only has gone stale.
        Assert.Empty(ServerHost.OperatorPaths.Except(routes, StringComparer.OrdinalIgnoreCase));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // What the settings page posts; same port, so the relay hot-reloads instead of restarting.
    private static string SettingsBody(int pollingIntervalMs) => $$"""
        {
          "host": "localhost",
          "proPresenterPort": 50001,
          "pollingIntervalMs": {{pollingIntervalMs}},
          "relayPort": {{RelayTestHost.Port}},
          "screenCaptureEnabled": true,
          "captureDisplayIndex": 0,
          "slideProMode": "off",
          "slideProApiKey": "{{RelayTestHost.SeedApiKey}}",
          "slideProPresentationId": ""
        }
        """;

    private static TheoryData<string> ToData(IEnumerable<string> values)
    {
        var data = new TheoryData<string>();
        foreach (var v in values) data.Add(v);
        return data;
    }

    private static TheoryData<string, string> Cross(IEnumerable<string> paths, IEnumerable<string> addresses)
    {
        var data = new TheoryData<string, string>();
        foreach (var p in paths)
            foreach (var a in addresses)
                data.Add(p, a);
        return data;
    }
}
