using Microsoft.Extensions.Options;
using SlideProRelay.Server.ProPresenter;
using SlideProRelay.Server.ProPresenter.Models;
using System.Threading.Channels;

namespace SlideProRelay.Server.ScreenCapture;

/// <summary>
/// Watches the live slide and, whenever the active slide changes, grabs a fresh
/// full-screen capture of the output display and stores it in the cache.
/// </summary>
/// <remarks>
/// Capture runs off the polling thread via a single-slot drop-oldest channel, so
/// a slow grab can never stall slide polling and only the newest pending slide
/// is ever captured.
/// </remarks>
public sealed class ScreenCaptureCoordinator : BackgroundService
{
    private readonly SlideCache _slides;
    private readonly IProPresenterClient _client;
    private readonly IScreenCaptureService _capture;
    private readonly ScreenCaptureCache _cache;
    private readonly IOptionsMonitor<ScreenCaptureOptions> _opts;
    private readonly ILogger<ScreenCaptureCoordinator> _logger;

    // Set while capture is on hold because no display may be captured (e.g. a
    // single-display machine in auto mode). Lets us log the hold once, not on
    // every slide change, and announce when capture resumes.
    private DisplaySelectionReason? _holdReason;

    public ScreenCaptureCoordinator(
        SlideCache slides,
        IProPresenterClient client,
        IScreenCaptureService capture,
        ScreenCaptureCache cache,
        IOptionsMonitor<ScreenCaptureOptions> opts,
        ILogger<ScreenCaptureCoordinator> logger)
    {
        _slides = slides;
        _client = client;
        _capture = capture;
        _cache = cache;
        _opts = opts;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_opts.CurrentValue.Enabled)
        {
            _logger.LogInformation("Screen capture disabled");
            return;
        }

        if (!_capture.IsSupported)
        {
            _logger.LogInformation("Screen capture not supported on this platform yet");
            return;
        }

        _logger.LogInformation("Screen capture started (captures on slide change)");

        // Single-slot, drop-oldest: if slides change faster than we can capture,
        // we only ever capture the most recent one.
        var signals = Channel.CreateBounded<string>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        string? lastKey = null;
        using var sub = _slides.Subscribe(status =>
        {
            var key = KeyFor(status);
            if (key is null)
            {
                // Nothing live (offline or cleared): drop the stale frame so
                // consumers don't keep showing the last slide.
                _cache.Clear();
                lastKey = null;
                return;
            }

            if (key != lastKey)
            {
                lastKey = key;
                signals.Writer.TryWrite(key);
            }
        });

        // If a slide is already live at startup, capture it immediately.
        if (_slides.Latest is { } latest && KeyFor(latest) is { } initial)
        {
            lastKey = initial;
            signals.Writer.TryWrite(initial);
        }

        try
        {
            await foreach (var key in signals.Reader.ReadAllAsync(ct))
            {
                var selection = await SelectDisplayAsync(ct);
                if (!selection.HasDisplay)
                {
                    // Nothing we may capture. Drop any stale frame so consumers
                    // get a 204 instead of an old slide, and say why once.
                    _cache.Clear();
                    if (_holdReason != selection.Reason)
                    {
                        _holdReason = selection.Reason;
                        _logger.LogInformation("Screen capture on hold: {Reason}", selection.Description);
                    }
                    continue;
                }

                if (_holdReason is not null)
                {
                    _holdReason = null;
                    _logger.LogInformation("Screen capture resumed: {Reason}", selection.Description);
                }

                var jpeg = await _capture.CaptureAsync(selection.Index, ct);
                if (jpeg is not null)
                    _cache.Set(jpeg, key);
                else
                    _logger.LogDebug("No frame captured for slide {Key}", key);
            }
        }
        catch (OperationCanceledException) { }

        _logger.LogInformation("Screen capture stopped");
    }

    // Picks which display to capture: a manual ScreenCapture:DisplayIndex wins
    // (and skips the ProPresenter round-trip); otherwise auto-match ProPresenter's
    // audience-screen resolution among the non-primary displays. Auto never picks
    // the primary display, so a single-display machine yields "no display" until
    // the operator chooses one explicitly.
    private async Task<DisplaySelection> SelectDisplayAsync(CancellationToken ct)
    {
        var configured = _opts.CurrentValue.DisplayIndex;
        var displays = _capture.GetDisplays();

        if (configured >= 1)
        {
            var manual = DisplaySelector.Resolve(displays, configured, null, null);
            if (manual.Reason == DisplaySelectionReason.Configured)
                return manual;
            // The configured display isn't attached (unplugged?): fall through
            // to auto, using the same inputs /api/displays reports.
        }

        var audience = await _client.GetAudienceScreenSizeAsync(ct);
        return DisplaySelector.Resolve(displays, configured, audience?.Width, audience?.Height);
    }

    // Identity of the live slide: its uuid (or text as a fallback). Null when
    // nothing is live, which signals "clear the cached frame".
    private static string? KeyFor(SlideStatus status)
    {
        if (status.Connection != ConnectionState.Connected || status.Current is null)
            return null;

        if (status.Current.Uuid is { Length: > 0 } uuid)
            return uuid;

        return status.Current.Text is { Length: > 0 } text ? "t:" + text : null;
    }
}
