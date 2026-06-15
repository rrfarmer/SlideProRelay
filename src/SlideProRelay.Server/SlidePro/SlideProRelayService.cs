using Microsoft.Extensions.Options;
using SlideProRelay.Server.ProPresenter;
using SlideProRelay.Server.ProPresenter.Models;
using SlideProRelay.Server.ScreenCapture;
using System.Threading.Channels;

namespace SlideProRelay.Server.SlidePro;

/// <summary>
/// Watches the live slide and relays each screen capture to SlidePro on slide change.
/// Re-presents slides already uploaded this session instead of uploading duplicates.
/// </summary>
public sealed class SlideProRelayService(
    SlideCache slides,
    ScreenCaptureCache captures,
    SlideProApiClient client,
    IOptionsMonitor<SlideProOptions> opts,
    ILogger<SlideProRelayService> logger) : BackgroundService
{
    // Maps slide key (UUID or "t:text") → SlidePro slideId for this session.
    private readonly Dictionary<string, int> _slideIds = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("SlidePro relay service started");

        var signals = Channel.CreateBounded<SlideStatus>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        // Only enqueue when the slide key actually changes — the poller fires every
        // 500 ms regardless of whether the slide moved, so without this guard we
        // would call /represent on every tick for the current slide.
        string? lastKey = null;
        using var sub = slides.Subscribe(status =>
        {
            var key = SlideKey(status);
            if (key == lastKey) return;
            lastKey = key;
            signals.Writer.TryWrite(status);
        });

        try
        {
            await foreach (var status in signals.Reader.ReadAllAsync(ct))
                await HandleSlideChangeAsync(status, ct);
        }
        catch (OperationCanceledException) { }

        logger.LogInformation("SlidePro relay service stopped");
    }

    private async Task HandleSlideChangeAsync(SlideStatus status, CancellationToken ct)
    {
        var o = opts.CurrentValue;

        if (o.SendTextUpdates && !string.IsNullOrWhiteSpace(o.ApiKey) && status.Current is not null)
            await client.PostSlideTextAsync(
                o.ApiKey,
                status.Current.Uuid,
                status.Current.Text,
                status.Next?.Text,
                status.UpdatedAt,
                ct);

        if (!o.Enabled
            || string.IsNullOrWhiteSpace(o.ApiKey)
            || string.IsNullOrWhiteSpace(o.PresentationId)
            || status.Current is null)
            return;

        var key = SlideKey(status);
        if (key is null) return;

        if (_slideIds.TryGetValue(key, out var existingId))
        {
            logger.LogDebug("SlidePro: re-presenting slide {Key} (slideId={Id})", key, existingId);
            await client.RepresentSlideAsync(o.ApiKey, o.PresentationId, existingId, ct);
            return;
        }

        var jpeg = await WaitForCaptureAsync(key, ct);
        if (jpeg is null)
        {
            logger.LogWarning("SlidePro: timed out waiting for screen capture for slide {Key} — is screen capture enabled?", key);
            return;
        }

        var result = await client.UploadSlideAsync(o.ApiKey, o.PresentationId, jpeg, presented: true, ct);
        if (result is not null)
        {
            _slideIds[key] = result.SlideId;
            logger.LogInformation("SlidePro: uploaded slide {Key} → slideId={Id}", key, result.SlideId);
        }
    }

    // Polls until ScreenCaptureCache has a frame for this slide key, or times out.
    // ScreenCaptureCoordinator captures on the same slide-change signal, so the
    // frame typically arrives within one capture cycle (~500 ms on most hardware).
    private async Task<byte[]?> WaitForCaptureAsync(string key, CancellationToken ct)
    {
        for (var i = 0; i < 20; i++)
        {
            var frame = captures.Latest;
            if (frame?.Key == key) return frame.Jpeg;
            await Task.Delay(500, ct);
        }
        return null;
    }

    private static string? SlideKey(SlideStatus status)
    {
        if (status.Current?.Uuid is { Length: > 0 } uuid) return uuid;
        return status.Current?.Text is { Length: > 0 } text ? "t:" + text : null;
    }
}
