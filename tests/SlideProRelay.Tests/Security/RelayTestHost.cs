using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SlideProRelay.Server;
using SlideProRelay.Server.ProPresenter;
using SlideProRelay.Server.ProPresenter.Models;
using SlideProRelay.Server.ScreenCapture;

namespace SlideProRelay.Tests.Security;

/// <summary>
/// xunit class fixture: the real relay pipeline (loopback filter, routing, CORS,
/// endpoints) on the in-memory TestServer. The relay's background services and
/// its ProPresenter / screen-capture dependencies are replaced, so requests are
/// deterministic and touch no hardware or network.
/// </summary>
public sealed class RelayTestHost : IAsyncLifetime
{
    public const int Port = 5174;
    public const string SeedApiKey = "sk-test-secret-key";

    private string _dir = null!;

    public WebApplication App { get; private set; } = null!;
    public string SettingsPath { get; private set; } = null!;
    public TestServer Server => App.GetTestServer();

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SlideProRelay.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        SettingsPath = Path.Combine(_dir, "settings.json");
        await File.WriteAllTextAsync(SettingsPath, SeedSettingsJson);

        App = ServerHost.Create(
            [],
            configOverrides:
            [
                new("Settings:FilePath", SettingsPath),
                new("Relay:Port", Port.ToString()),
            ],
            configureBuilder: b =>
            {
                b.WebHost.UseTestServer();

                var services = b.Services;

                // Drop the relay's own background services: they poll ProPresenter,
                // upload to SlidePro and capture screens, none of which matters here.
                foreach (var d in services.Where(IsRelayBackgroundService).ToList())
                    services.Remove(d);

                services.RemoveAll<IProPresenterClient>();
                services.AddSingleton<IProPresenterClient, OfflineProPresenterClient>();

                services.RemoveAll<IScreenCaptureService>();
                services.AddSingleton<IScreenCaptureService, NullScreenCaptureService>();
            });

        await App.StartAsync();

        // A live slide, so /events has something to send straight away.
        App.Services.GetRequiredService<SlideCache>().Update(new SlideStatus(
            new SlideInfo("slide-1", "Amazing grace", null),
            new SlideInfo("slide-2", "How sweet the sound", null),
            ConnectionState.Connected,
            DateTimeOffset.UtcNow));
    }

    public async Task DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>
    /// Runs a request through the whole pipeline as if it arrived from
    /// <paramref name="remote"/>. Completes once the response has started, so
    /// streaming endpoints such as /events can be checked too.
    /// </summary>
    public Task<HttpContext> SendAsync(
        string path,
        IPAddress? remote,
        string method = "GET",
        string? jsonBody = null,
        string? origin = null,
        CancellationToken ct = default) =>
        Server.SendAsync(ctx =>
        {
            ctx.Request.Method = method;
            ctx.Request.Path = path;
            ctx.Connection.RemoteIpAddress = remote;

            if (origin is not null)
                ctx.Request.Headers.Origin = origin;

            if (jsonBody is not null)
            {
                var bytes = Encoding.UTF8.GetBytes(jsonBody);
                ctx.Request.ContentType = "application/json";
                ctx.Request.ContentLength = bytes.Length;
                ctx.Request.Body = new MemoryStream(bytes);
                // Kestrel sets this; without it minimal API body binding assumes there is no body.
                ctx.Features.Set<IHttpRequestBodyDetectionFeature>(RequestHasBody.Instance);
            }
        }, ct);

    public static async Task<string> ReadBodyAsync(HttpContext ctx)
    {
        using var reader = new StreamReader(ctx.Response.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static bool IsRelayBackgroundService(ServiceDescriptor d) =>
        d.ServiceType == typeof(IHostedService)
        && d.ImplementationType?.Namespace?.StartsWith("SlideProRelay.", StringComparison.Ordinal) == true;

    private static readonly string SeedSettingsJson = $$"""
        {
          "Host": "localhost",
          "ProPresenterPort": 50001,
          "PollingIntervalMs": 500,
          "RelayPort": {{Port}},
          "ScreenCaptureEnabled": true,
          "CaptureDisplayIndex": 0,
          "SlideProMode": "off",
          "SlideProApiKey": "{{SeedApiKey}}",
          "SlideProPresentationId": ""
        }
        """;

    private sealed class RequestHasBody : IHttpRequestBodyDetectionFeature
    {
        public static readonly RequestHasBody Instance = new();
        public bool CanHaveBody => true;
    }

    /// <summary>ProPresenter is unreachable: every call reports "nothing live".</summary>
    private sealed class OfflineProPresenterClient : IProPresenterClient
    {
        public Task<string?> GetVersionAsync(CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task<SlideStatus> GetCurrentSlideAsync(CancellationToken ct = default) =>
            Task.FromResult(new SlideStatus(null, null, ConnectionState.Unavailable, DateTimeOffset.UtcNow));

        public Task<string> GetRawSlideJsonAsync(CancellationToken ct = default) =>
            Task.FromResult("{}");

        public Task<byte[]?> GetCurrentSlideImageAsync(int quality, CancellationToken ct = default) =>
            Task.FromResult<byte[]?>(null);

        public Task<(int Width, int Height)?> GetAudienceScreenSizeAsync(CancellationToken ct = default) =>
            Task.FromResult<(int Width, int Height)?>(null);
    }
}
