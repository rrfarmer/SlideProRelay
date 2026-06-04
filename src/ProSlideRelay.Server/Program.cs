using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using ProSlideRelay.Server.ProPresenter;
using ProSlideRelay.Server.ProPresenter.Models;
using ProSlideRelay.Server.Startup;

var camelCaseJson = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

var builder = WebApplication.CreateBuilder(args);

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

// ── API ──────────────────────────────────────────────────────────────────────

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Diagnostic: returns exactly what ProPresenter sends, unmodified
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

    return Results.Ok(SlidePayload(status));
});

// ── Server-Sent Events ───────────────────────────────────────────────────────

app.MapGet("/events", async (SlideCache cache, HttpContext ctx) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";

    // Disable response buffering so events reach the browser immediately
    ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

    var ct = ctx.RequestAborted;
    var channel = System.Threading.Channels.Channel.CreateBounded<SlideStatus>(
        new System.Threading.Channels.BoundedChannelOptions(4)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest
        });

    // Send current state immediately so the browser has something on connect
    if (cache.Latest is { } latest)
        await WriteSseEvent(ctx.Response, latest, ct);

    using var sub = cache.Subscribe(status => channel.Writer.TryWrite(status));

    using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(15));
    var heartbeatTask = Task.Run(async () =>
    {
        while (!ct.IsCancellationRequested && await heartbeat.WaitForNextTickAsync(ct).ConfigureAwait(false))
            channel.Writer.TryWrite(cache.Latest ?? new SlideStatus(null, null, ConnectionState.Unavailable, DateTimeOffset.UtcNow));
    }, ct);

    try
    {
        await foreach (var status in channel.Reader.ReadAllAsync(ct))
            await WriteSseEvent(ctx.Response, status, ct);
    }
    catch (OperationCanceledException) { }
});

async Task WriteSseEvent(HttpResponse response, SlideStatus status, CancellationToken ct)
{
    var json = JsonSerializer.Serialize(SlidePayload(status), camelCaseJson);
    var line = $"data: {json}\n\n";
    await response.WriteAsync(line, ct);
    await response.Body.FlushAsync(ct);
}

static object SlidePayload(SlideStatus status) => new
{
    connection = status.Connection.ToString().ToLowerInvariant(),
    current = status.Current is null ? null : new { status.Current.Uuid, status.Current.Text, status.Current.Notes },
    next = status.Next is null ? null : new { status.Next.Uuid, status.Next.Text, status.Next.Notes },
    updatedAt = status.UpdatedAt,
};

// ── Browser page ─────────────────────────────────────────────────────────────

app.MapGet("/", () => Results.Content(HtmlPage(), "text/html"));

static string HtmlPage() => """
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8">
      <meta name="viewport" content="width=device-width, initial-scale=1">
      <title>ProSlideRelay</title>
      <style>
        *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

        body {
          background: #000;
          color: #fff;
          font-family: system-ui, sans-serif;
          display: flex;
          flex-direction: column;
          min-height: 100dvh;
          padding: 1.5rem;
        }

        #status {
          font-size: 0.75rem;
          color: #888;
          text-align: right;
          margin-bottom: 1rem;
          min-height: 1rem;
          letter-spacing: 0.03em;
        }
        #status.unavailable { color: #c44; }
        #status.connected   { color: #4c4; }

        #current {
          flex: 1;
          display: flex;
          align-items: center;
          justify-content: center;
          text-align: center;
          font-size: clamp(1.5rem, 5vw, 3rem);
          font-weight: 600;
          line-height: 1.3;
          white-space: pre-wrap;
          word-break: break-word;
          padding: 1rem;
        }

        #next {
          text-align: center;
          font-size: clamp(0.85rem, 2.5vw, 1.25rem);
          color: #555;
          padding: 1rem;
          min-height: 3rem;
          white-space: pre-wrap;
          word-break: break-word;
        }
      </style>
    </head>
    <body>
      <div id="status">Connecting…</div>
      <div id="current"></div>
      <div id="next"></div>

      <script>
        const statusEl  = document.getElementById('status');
        const currentEl = document.getElementById('current');
        const nextEl    = document.getElementById('next');

        function connect() {
          const es = new EventSource('/events');

          es.onmessage = (e) => {
            const d = JSON.parse(e.data);
            const connected = d.connection === 'connected';
            statusEl.textContent = connected
              ? (d.current?.text ? '● Live' : '● Live — no slide text')
              : '● ProPresenter offline';
            statusEl.className   = d.connection;
            currentEl.textContent = d.current?.text ?? '';
            nextEl.textContent    = d.next?.text ? 'Next: ' + d.next.text : '';
          };

          es.onerror = () => {
            statusEl.textContent = 'Reconnecting…';
            statusEl.className   = 'unavailable';
            es.close();
            setTimeout(connect, 3000);
          };
        }

        connect();
      </script>
    </body>
    </html>
    """;

app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var addresses = app.Urls.Count > 0
        ? app.Urls
        : ["http://localhost:5174"];
    NetworkUrlPrinter.Print(logger, addresses);
});

app.Run();
