using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using SlideProRelay.Server.ProPresenter;
using SlideProRelay.Server.ProPresenter.Models;
using SlideProRelay.Server.Startup;

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

        builder.Services.AddHttpClient<IProPresenterClient, ProPresenterClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProPresenterOptions>>().Value;
            client.BaseAddress = new Uri($"http://{opts.Host}:{opts.Port}");
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
