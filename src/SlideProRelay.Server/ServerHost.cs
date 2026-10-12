using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using QRCoder;
using SlideProRelay.Server.ProPresenter;
using SlideProRelay.Server.ProPresenter.Models;
using SlideProRelay.Server.ScreenCapture;
using SlideProRelay.Server.Security;
using SlideProRelay.Server.Settings;
using SlideProRelay.Server.SlidePro;
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
    /// Routes served only to this machine (see <see cref="LoopbackOnly"/>): the
    /// settings page and API, which expose the SlidePro API key and can restart
    /// the relay, plus operator diagnostics. Every other route stays open to the
    /// LAN because phones need it. An operator-only route added to
    /// <see cref="MapEndpoints"/> must be listed here as well.
    /// </summary>
    public static readonly IReadOnlyList<string> OperatorPaths =
    [
        "/settings",
        "/api/settings",
        "/api/network-url",
        "/api/slidepro/presentations",
        "/api/displays",
        "/api/raw",
    ];

    // The one cross-origin call the relay expects: after a port change the
    // settings page polls /api/health on the new port (see below).
    private const string HealthPollCors = "HealthPoll";

    /// <summary>
    /// Creates and configures the WebApplication but does not start it.
    /// Call RunAsync() for CLI use or StartAsync() for hosted (tray) use.
    /// <paramref name="configureBuilder"/> runs just before the app is built;
    /// tests use it to swap Kestrel for the in-memory TestServer.
    /// </summary>
    public static WebApplication Create(
        string[] args,
        IEnumerable<KeyValuePair<string, string?>>? configOverrides = null,
        Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        var mutableCfg = new MutableConfigSource();

        if (configOverrides is not null)
            builder.Configuration.AddInMemoryCollection(configOverrides);

        // Added after AddInMemoryCollection so its values take precedence on hot-reload.
        builder.Configuration.Sources.Add(mutableCfg);

        var relayPort = builder.Configuration.GetValue<int>("Relay:Port", 5174);
        builder.WebHost.UseUrls($"http://*:{relayPort}");

        // CORS is needed for exactly one call: after a port change the settings
        // page, still open on http://localhost:<old port>, polls /api/health on
        // the new port until the relay is back. Only that endpoint carries the
        // policy, and only loopback origins are allowed. Nothing else is
        // cross-origin: the phone page uses same-origin requests.
        builder.Services.AddCors(o => o.AddPolicy(HealthPollCors, p =>
            p.SetIsOriginAllowed(LoopbackOnly.IsLoopbackOrigin).WithMethods("GET")));

        builder.Services.Configure<ProPresenterOptions>(
            builder.Configuration.GetSection(ProPresenterOptions.SectionName));

        builder.Services.Configure<ScreenCaptureOptions>(
            builder.Configuration.GetSection(ScreenCaptureOptions.SectionName));

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
        builder.Services.AddSingleton<SlideHistory>();
        builder.Services.AddHostedService<SlidePollingService>();

        builder.Services.Configure<SlideProOptions>(
            builder.Configuration.GetSection(SlideProOptions.SectionName));

        // Named client with a longer timeout to accommodate image uploads.
        builder.Services.AddHttpClient("SlidePro", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<SlideProApiClient>();
        builder.Services.AddHostedService<SlideProRelayService>();

        // Screen capture: real output-display pixels, served from /api/screen-capture.
        // Platform impl is selected at runtime, mirroring the port detector above.
        // Windows uses the Null impl until the DXGI capture path lands.
        builder.Services.AddSingleton<ScreenCaptureCache>();
        builder.Services.AddSingleton<IScreenCaptureService>(sp =>
            OperatingSystem.IsMacOS()
                ? new MacScreenCaptureService(sp.GetRequiredService<ILogger<MacScreenCaptureService>>())
                : OperatingSystem.IsWindows()
                    ? new WindowsScreenCaptureService(sp.GetRequiredService<ILogger<WindowsScreenCaptureService>>())
                    : new NullScreenCaptureService());
        builder.Services.AddHostedService<ScreenCaptureCoordinator>();

        // Settings service — reads/writes the JSON settings file supplied by the host app.
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<RestartSignal>();
        builder.Services.AddSingleton(mutableCfg);

        configureBuilder?.Invoke(builder);

        var app = builder.Build();

        // Operator-only routes are hidden from the LAN before routing runs, so a
        // non-loopback caller gets an empty 404 whatever the method or body.
        app.UseLoopbackOnly(OperatorPaths);
        app.UseRouting();
        app.UseCors();

        MapEndpoints(app);

        return app;
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
           .RequireCors(HealthPollCors);

        // Operator diagnostic: loopback only (see OperatorPaths).
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

        // Full-screen JPEG of the real output display, captured on each slide
        // change (see ScreenCaptureCoordinator). Separate from /api/slide-image,
        // which is ProPresenter's rendered thumbnail. 204 when nothing has been
        // captured yet, capture is disabled, or the platform isn't supported.
        app.MapGet("/api/screen-capture", (ScreenCaptureCache cache) =>
        {
            var frame = cache.Latest;
            return frame is null
                ? Results.NoContent()
                : Results.File(frame.Jpeg, "image/jpeg");
        });

        // Tiny companion to /api/screen-capture: the slide key + timestamp of the
        // currently cached frame. The web UI polls this (cheap JSON) so it only
        // swaps in the image once the capture for the *current* slide is ready,
        // avoiding a one-slide-behind flash. 204 when nothing is cached.
        app.MapGet("/api/screen-capture/key", (ScreenCaptureCache cache) =>
        {
            var frame = cache.Latest;
            return frame is null
                ? Results.NoContent()
                : Results.Ok(new { key = frame.Key, capturedAt = frame.CapturedAt });
        });

        // Lists capturable displays + ProPresenter's audience-screen size and which
        // display the relay would capture. Powers the settings page "Capture
        // display" picker and is a handy diagnostic for multi-monitor setups.
        // selectedIndex is 0 when nothing will be captured (auto mode never picks
        // the primary display); `reason` says why in operator-facing terms.
        app.MapGet("/api/displays", async (
            IScreenCaptureService capture,
            IProPresenterClient client,
            IOptionsMonitor<ScreenCaptureOptions> opts,
            CancellationToken ct) =>
        {
            var displays = capture.GetDisplays();
            var audience = await client.GetAudienceScreenSizeAsync(ct);
            var configured = opts.CurrentValue.DisplayIndex;
            var selected = DisplaySelector.Resolve(displays, configured, audience?.Width, audience?.Height);

            // What "Auto" would do right now, so the picker can explain that
            // option even while a manual index is configured.
            var auto = configured >= 1
                ? DisplaySelector.Resolve(displays, 0, audience?.Width, audience?.Height)
                : selected;

            return Results.Ok(new
            {
                supported = capture.IsSupported,
                configuredIndex = configured, // 0 = auto
                selectedIndex = selected.Index, // 0 = nothing will be captured
                reason = selected.Description,
                autoIndex = auto.Index,
                autoReason = auto.Description,
                audience = audience is null ? null : new { audience.Value.Width, audience.Value.Height },
                displays = displays.Select(d => new
                {
                    d.Index,
                    d.Width,
                    d.Height,
                    d.X,
                    d.Y,
                    d.IsPrimary,
                    matchesAudience = audience is not null && !d.IsPrimary
                        && d.Width == audience.Value.Width && d.Height == audience.Value.Height,
                }),
            });
        });

        app.MapGet("/api/current", (SlideCache cache, SlideHistory history) =>
        {
            var status = cache.Latest;
            if (status is null)
                return Results.Ok(new { connection = "starting", current = (object?)null, next = (object?)null });
            return Results.Ok(BuildPayload(status, history));
        });

        app.MapGet("/events", async (SlideCache cache, SlideHistory history, HttpContext ctx) =>
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
                await SendEvent(ctx.Response, latest, history, ct);

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
                    await SendEvent(ctx.Response, status, history, ct);
            }
            catch (OperationCanceledException) { }
        });

        // Proxies to SlidePro's /presentations endpoint so the tray settings UI can
        // populate a dropdown without knowing the base URL. apiKey can be passed as a
        // query param so the user can refresh the list before saving their key.
        // Loopback only (see OperatorPaths): the key travels in the query string.
        app.MapGet("/api/slidepro/presentations", async (
            SlideProApiClient slideProClient,
            IOptionsMonitor<SlideProOptions> slideProOpts,
            string? apiKey,
            CancellationToken ct) =>
        {
            var key = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : slideProOpts.CurrentValue.ApiKey;
            if (string.IsNullOrWhiteSpace(key))
                return Results.BadRequest(new { message = "No API key provided." });

            var presentations = await slideProClient.GetPresentationsAsync(key, ct);
            return presentations is null
                ? Results.StatusCode(502)
                : Results.Ok(presentations);
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

        // ── Settings web UI + API — loopback only (see OperatorPaths) ──────────

        app.MapGet("/settings", () => Results.Content(SettingsPage.Content, "text/html"));

        // Returns the LAN and localhost URLs for the phone display page.
        app.MapGet("/api/network-url", (IConfiguration config) =>
        {
            var port = config.GetValue<int>("Relay:Port", 5174);
            var lan  = NetworkUrlPrinter.GetLanIp();
            return Results.Ok(new
            {
                local   = $"http://localhost:{port}",
                network = lan is not null ? $"http://{lan}:{port}" : (string?)null,
            });
        });

        app.MapGet("/api/settings", (SettingsService settingsSvc) =>
        {
            if (!settingsSvc.IsConfigured)
                return Results.Problem("Settings file path not configured.", statusCode: 503);
            return Results.Ok(settingsSvc.Load() ?? new RelaySettings());
        });

        app.MapPost("/api/settings", (
            RelaySettings body,
            SettingsService settingsSvc,
            MutableConfigSource mutableCfg,
            RestartSignal restart,
            IConfiguration config) =>
        {
            if (!settingsSvc.IsConfigured)
                return Results.Problem("Settings file path not configured.", statusCode: 503);

            settingsSvc.Save(body);

            var currentPort = config.GetValue<int>("Relay:Port", 5174);
            if (body.RelayPort != currentPort)
            {
                // Port change requires a new socket binding — full restart.
                _ = Task.Delay(250).ContinueWith(_ => restart.SignalRestart());
                return Results.Ok(new { saved = true, newPort = body.RelayPort, restarting = true });
            }

            // Everything else is consumed via IOptionsMonitor<T> — hot-reload in place.
            mutableCfg.Update(body.ToConfigOverrides());
            return Results.Ok(new { saved = true, newPort = body.RelayPort, restarting = false });
        });
    }

    private static async Task SendEvent(HttpResponse response, SlideStatus status, SlideHistory history, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(BuildPayload(status, history), CamelCaseJson);
        await response.WriteAsync($"data: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }

    private static object BuildPayload(SlideStatus status, SlideHistory history)
    {
        // How many times the live slide has been shown — seenCount > 1 means it's
        // a repeat (e.g. a chorus coming back around). Additive fields; existing
        // clients simply ignore them.
        var seenCount = history.SeenCount(status.Current?.Uuid);

        return new
        {
            connection = status.Connection.ToString().ToLowerInvariant(),
            current = status.Current is null ? null : new
            {
                status.Current.Uuid,
                status.Current.Text,
                status.Current.Notes,
                seenCount,
                isRepeat = seenCount > 1,
            },
            next = status.Next is null ? null : new { status.Next.Uuid, status.Next.Text, status.Next.Notes },
            updatedAt = status.UpdatedAt,
        };
    }
}
