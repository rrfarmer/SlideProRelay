using Microsoft.AspNetCore.Http.Features;
using QRCoder;
using SlideProRelay.Server.ProPresenter;
using SlideProRelay.Server.ProPresenter.Models;
using SlideProRelay.Server.Startup;
using System.Text.Json;

namespace SlideProRelay.Server;

public static class ServerHost
{
    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates and configures the WebApplication but does not start it.
    /// Call RunAsync() for CLI use or StartAsync() for hosted (tray) use.
    /// </summary>
    public static WebApplication Create(
        string[] args,
        IEnumerable<KeyValuePair<string, string?>>? configOverrides = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (configOverrides is not null)
            builder.Configuration.AddInMemoryCollection(configOverrides);

        var relayPort = builder.Configuration.GetValue<int>("Relay:Port", 5174);
        builder.WebHost.UseUrls($"http://*:{relayPort}");

        builder.Services.Configure<ProPresenterOptions>(
            builder.Configuration.GetSection(ProPresenterOptions.SectionName));

        // Port auto-detection (reads ProPresenter's own persisted port). The base
        // URI is resolved dynamically per request via ProPresenterEndpoint, so the
        // relay follows ProPresenter's shifting port without a fixed BaseAddress.
        builder.Services.AddSingleton<IProPresenterPortDetector>(sp =>
            OperatingSystem.IsMacOS()
                ? new MacProPresenterPortDetector(sp.GetRequiredService<ILogger<MacProPresenterPortDetector>>())
                : OperatingSystem.IsWindows()
                    ? new WindowsProPresenterPortDetector(sp.GetRequiredService<ILogger<WindowsProPresenterPortDetector>>())
                    : new NullProPresenterPortDetector());

        builder.Services.AddSingleton<ProPresenterEndpoint>();

        builder.Services.AddHttpClient<IProPresenterClient, ProPresenterClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        builder.Services.AddSingleton<SlideCache>();
        builder.Services.AddHostedService<SlidePollingService>();

        var app = builder.Build();

        MapEndpoints(app);

        return app;
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/api/raw", async (IProPresenterClient client) =>
        {
            var raw = await client.GetRawSlideJsonAsync();
            return Results.Content(raw, "application/json");
        });

        // EXPERIMENTAL: JPEG thumbnail of the current slide/cue (e.g. an image
        // slide with no text). quality = pixels on the longest edge (default 1920
        // ≈ 1080p). 204 when nothing is live or ProPresenter is unreachable.
        app.MapGet("/api/slide-image", async (IProPresenterClient client, int? quality, CancellationToken ct) =>
        {
            var q = quality is > 0 and <= 3840 ? quality.Value : 1920;
            var bytes = await client.GetCurrentSlideImageAsync(q, ct);
            return bytes is null
                ? Results.NoContent()
                : Results.File(bytes, "image/jpeg");
        });

        app.MapGet("/api/current", (SlideCache cache) =>
        {
            var status = cache.Latest;
            if (status is null)
                return Results.Ok(new { connection = "starting", current = (object?)null, next = (object?)null });
            return Results.Ok(BuildPayload(status));
        });

        app.MapGet("/events", async (SlideCache cache, HttpContext ctx) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";
            ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            var ct = ctx.RequestAborted;
            var channel = System.Threading.Channels.Channel.CreateBounded<SlideStatus>(
                new System.Threading.Channels.BoundedChannelOptions(4)
                {
                    FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest
                });

            if (cache.Latest is { } latest)
                await SendEvent(ctx.Response, latest, ct);

            using var sub = cache.Subscribe(status => channel.Writer.TryWrite(status));

            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(15));
            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested && await heartbeat.WaitForNextTickAsync(ct).ConfigureAwait(false))
                    channel.Writer.TryWrite(cache.Latest ?? new SlideStatus(null, null, ConnectionState.Unavailable, DateTimeOffset.UtcNow));
            }, ct);

            try
            {
                await foreach (var status in channel.Reader.ReadAllAsync(ct))
                    await SendEvent(ctx.Response, status, ct);
            }
            catch (OperationCanceledException) { }
        });

        app.MapGet("/api/qr", (IConfiguration config) =>
        {
            var relayPort = config.GetValue<int>("Relay:Port", 5174);
            var lan = NetworkUrlPrinter.GetLanIp();
            var url = lan is not null
                ? $"http://{lan}:{relayPort}"
                : $"http://localhost:{relayPort}";

            var data = new QRCodeGenerator().CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data).GetGraphic(8);
            return Results.File(png, "image/png");
        });

        app.MapGet("/", () => Results.Content(HtmlPage.Content, "text/html"));
    }

    private static async Task SendEvent(HttpResponse response, SlideStatus status, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(BuildPayload(status), CamelCaseJson);
        await response.WriteAsync($"data: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }

    private static object BuildPayload(SlideStatus status) => new
    {
        connection = status.Connection.ToString().ToLowerInvariant(),
        current = status.Current is null ? null : new { status.Current.Uuid, status.Current.Text, status.Current.Notes },
        next = status.Next is null ? null : new { status.Next.Uuid, status.Next.Text, status.Next.Notes },
        updatedAt = status.UpdatedAt,
    };
}
