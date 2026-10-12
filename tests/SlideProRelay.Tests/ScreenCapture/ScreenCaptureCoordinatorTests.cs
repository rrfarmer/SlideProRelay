using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlideProRelay.Server.ProPresenter;
using SlideProRelay.Server.ProPresenter.Models;
using SlideProRelay.Server.ScreenCapture;

namespace SlideProRelay.Tests.ScreenCapture;

/// <summary>
/// End-to-end behaviour of the coordinator around display selection: what it
/// captures, when it refuses to, what it logs, and what it leaves in the cache.
/// </summary>
public sealed class ScreenCaptureCoordinatorTests
{
    private static readonly CaptureDisplay Primary = new(1, 1920, 1080, 0, 0, IsPrimary: true);
    private static readonly CaptureDisplay Secondary = new(2, 1920, 1080, 1920, 0, IsPrimary: false);

    [Fact]
    public async Task Auto_SingleDisplay_CapturesNothing_AndLogsHoldOnce()
    {
        await using var h = new Harness(configuredIndex: 0, Primary);
        await h.StartAsync();

        await h.ShowSlideAsync("a");
        await h.ShowSlideAsync("b");
        await h.ShowSlideAsync("c"); // proves "a" and "b" were fully handled

        Assert.Equal(0, h.Capture.CaptureCalls);
        Assert.Null(h.Frames.Latest);

        var holds = h.Log.Entries.Where(e => e.Message.Contains("on hold")).ToList();
        var hold = Assert.Single(holds);
        Assert.Equal(LogLevel.Information, hold.Level);
        Assert.Contains("No second display; pick one to capture", hold.Message);
    }

    [Fact]
    public async Task ExplicitIndex_SingleDisplay_CapturesThePrimary()
    {
        await using var h = new Harness(configuredIndex: 1, Primary);
        await h.StartAsync();

        await h.ShowSlideAsync("a");
        await h.WaitUntilAsync(() => h.Frames.Latest is not null, "a frame for slide a");

        Assert.Equal([1], h.Capture.RequestedIndices);
        Assert.Equal("a", h.Frames.Latest!.Key);
        Assert.DoesNotContain(h.Log.Entries, e => e.Message.Contains("on hold"));
    }

    [Fact]
    public async Task Auto_WithSecondDisplay_CapturesIt()
    {
        await using var h = new Harness(configuredIndex: 0, Primary, Secondary);
        await h.StartAsync();

        await h.ShowSlideAsync("a");
        await h.WaitUntilAsync(() => h.Frames.Latest is not null, "a frame for slide a");

        Assert.Equal([2], h.Capture.RequestedIndices);
        Assert.DoesNotContain(h.Log.Entries, e => e.Message.Contains("on hold"));
    }

    [Fact]
    public async Task SecondDisplayUnplugged_DropsStaleFrame_ThenResumesWhenReattached()
    {
        await using var h = new Harness(configuredIndex: 0, Primary, Secondary);
        await h.StartAsync();

        await h.ShowSlideAsync("a");
        await h.WaitUntilAsync(() => h.Frames.Latest?.Key == "a", "a frame for slide a");

        // Second display goes away mid-service: the next slide must not be served
        // from the stale frame, and nothing may be captured from the primary.
        h.Capture.Displays = [Primary];
        await h.ShowSlideAsync("b");
        await h.WaitUntilAsync(() => h.Frames.Latest is null, "the stale frame to be dropped");
        await h.ShowSlideAsync("c");
        Assert.Equal([2], h.Capture.RequestedIndices);
        Assert.Single(h.Log.Entries, e => e.Message.Contains("on hold"));

        // Plugged back in: capture resumes and says so once.
        h.Capture.Displays = [Primary, Secondary];
        await h.ShowSlideAsync("d");
        await h.WaitUntilAsync(() => h.Frames.Latest?.Key == "d", "a frame for slide d");
        Assert.Equal([2, 2], h.Capture.RequestedIndices);
        Assert.Single(h.Log.Entries, e => e.Message.Contains("resumed") && e.Level == LogLevel.Information);
    }

    // ── Harness + test doubles ─────────────────────────────────────────────────

    private sealed class Harness : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(10));

        public SlideCache Slides { get; } = new();
        public ScreenCaptureCache Frames { get; } = new();
        public FakeCaptureService Capture { get; }
        public RecordingLogger Log { get; } = new();
        public ScreenCaptureCoordinator Service { get; }

        public Harness(int configuredIndex, params CaptureDisplay[] displays)
        {
            Capture = new FakeCaptureService(displays);
            Service = new ScreenCaptureCoordinator(
                Slides,
                new FakeProPresenterClient(),
                Capture,
                Frames,
                new StaticOptions(new ScreenCaptureOptions { DisplayIndex = configuredIndex }),
                Log);
        }

        public Task StartAsync() => Service.StartAsync(_cts.Token);

        // Pushes a live slide and waits until the coordinator has started handling
        // it (it enumerates displays first thing). Because the coordinator handles
        // slides strictly one at a time, this also proves the previous slide was
        // fully handled.
        public async Task ShowSlideAsync(string uuid)
        {
            var before = Capture.DisplayQueries;
            Slides.Update(new SlideStatus(
                new SlideInfo(uuid, "Slide " + uuid, null), null, ConnectionState.Connected, DateTimeOffset.UtcNow));
            await WaitUntilAsync(() => Capture.DisplayQueries > before, $"slide {uuid} to be picked up");
        }

        public async Task WaitUntilAsync(Func<bool> condition, string what)
        {
            while (!condition())
            {
                if (_cts.IsCancellationRequested)
                    throw new TimeoutException("Timed out waiting for " + what);
                await Task.Delay(10, CancellationToken.None);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            await Service.StopAsync(CancellationToken.None);
            _cts.Dispose();
        }
    }

    private sealed class FakeCaptureService(IReadOnlyList<CaptureDisplay> displays) : IScreenCaptureService
    {
        private readonly List<int> _requested = [];
        private readonly Lock _lock = new();
        private int _displayQueries;

        public volatile IReadOnlyList<CaptureDisplay> Displays = displays;

        public bool IsSupported => true;

        public int DisplayQueries => Volatile.Read(ref _displayQueries);

        public int CaptureCalls { get { lock (_lock) return _requested.Count; } }

        public int[] RequestedIndices { get { lock (_lock) return [.. _requested]; } }

        public IReadOnlyList<CaptureDisplay> GetDisplays()
        {
            Interlocked.Increment(ref _displayQueries);
            return Displays;
        }

        public Task<byte[]?> CaptureAsync(int displayIndex, CancellationToken ct = default)
        {
            lock (_lock) _requested.Add(displayIndex);
            return Task.FromResult<byte[]?>([0xFF, 0xD8, 0xFF]); // JPEG magic bytes
        }
    }

    private sealed class FakeProPresenterClient : IProPresenterClient
    {
        public Task<string?> GetVersionAsync(CancellationToken ct = default) =>
            Task.FromResult<string?>("ProPresenter");

        public Task<SlideStatus> GetCurrentSlideAsync(CancellationToken ct = default) =>
            Task.FromResult(new SlideStatus(null, null, ConnectionState.Unavailable, DateTimeOffset.UtcNow));

        public Task<string> GetRawSlideJsonAsync(CancellationToken ct = default) =>
            Task.FromResult("{}");

        public Task<byte[]?> GetCurrentSlideImageAsync(int quality, CancellationToken ct = default) =>
            Task.FromResult<byte[]?>(null);

        public Task<(int Width, int Height)?> GetAudienceScreenSizeAsync(CancellationToken ct = default) =>
            Task.FromResult<(int, int)?>(null);
    }

    private sealed class StaticOptions(ScreenCaptureOptions value) : IOptionsMonitor<ScreenCaptureOptions>
    {
        public ScreenCaptureOptions CurrentValue { get; } = value;
        public ScreenCaptureOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ScreenCaptureOptions, string?> listener) => null;
    }

    private sealed class RecordingLogger : ILogger<ScreenCaptureCoordinator>
    {
        private readonly List<(LogLevel Level, string Message)> _entries = [];
        private readonly Lock _lock = new();

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get { lock (_lock) return [.. _entries]; }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_lock) _entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
