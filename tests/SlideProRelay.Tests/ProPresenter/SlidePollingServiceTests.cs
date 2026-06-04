using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SlideProRelay.Server.ProPresenter;
using SlideProRelay.Server.ProPresenter.Models;

namespace SlideProRelay.Tests.ProPresenter;

public sealed class SlidePollingServiceTests
{
    private static IOptionsMonitor<ProPresenterOptions> BuildOptions(int intervalMs = 50) =>
        new StaticOptionsMonitor(new ProPresenterOptions { PollingIntervalMs = intervalMs });

    [Fact]
    public async Task ExecuteAsync_PopulatesCache_AfterFirstPoll()
    {
        var expected = new SlideStatus(
            new SlideInfo("uuid-1", "Hello world", null),
            null,
            ConnectionState.Connected,
            DateTimeOffset.UtcNow);

        var client = new FakeProPresenterClient(expected);
        var cache = new SlideCache();
        var service = new SlidePollingService(
            client, cache, BuildOptions(), NullLogger<SlidePollingService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var task = service.StartAsync(cts.Token);

        // Wait until cache is populated
        while (cache.Latest is null && !cts.IsCancellationRequested)
            await Task.Delay(10, cts.Token);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.NotNull(cache.Latest);
        Assert.Equal(ConnectionState.Connected, cache.Latest.Connection);
        Assert.Equal("Hello world", cache.Latest.Current?.Text);
    }

    [Fact]
    public async Task ExecuteAsync_KeepsCacheUpdated_WhenPollingMultipleTimes()
    {
        var responses = new Queue<SlideStatus>(new[]
        {
            new SlideStatus(new SlideInfo("uuid-1", "Slide 1", null), null, ConnectionState.Connected, DateTimeOffset.UtcNow),
            new SlideStatus(new SlideInfo("uuid-2", "Slide 2", null), null, ConnectionState.Connected, DateTimeOffset.UtcNow),
        });

        var client = new FakeProPresenterClient(responses);
        var cache = new SlideCache();
        var service = new SlidePollingService(
            client, cache, BuildOptions(intervalMs: 20), NullLogger<SlidePollingService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        // Wait until the second slide appears in the cache
        while (cache.Latest?.Current?.Text != "Slide 2" && !cts.IsCancellationRequested)
            await Task.Delay(10, cts.Token);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("Slide 2", cache.Latest?.Current?.Text);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotThrow_WhenClientReturnsUnavailable()
    {
        var unavailable = new SlideStatus(null, null, ConnectionState.Unavailable, DateTimeOffset.UtcNow);
        var client = new FakeProPresenterClient(unavailable);
        var cache = new SlideCache();
        var service = new SlidePollingService(
            client, cache, BuildOptions(), NullLogger<SlidePollingService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await service.StartAsync(cts.Token);
        await Task.Delay(100, CancellationToken.None);
        await cts.CancelAsync();

        // Should complete without throwing
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(ConnectionState.Unavailable, cache.Latest?.Connection);
    }

    [Theory]
    [InlineData(0, 500,   500)]   // no failures → base interval
    [InlineData(1, 500,   500)]   // first failure → base interval
    [InlineData(2, 500,  1000)]   // second failure → 2x
    [InlineData(3, 500,  2000)]   // third failure → 4x
    [InlineData(4, 500,  4000)]   // fourth failure → 8x
    [InlineData(10, 500, 30000)]  // many failures → capped at 30 s
    public void CalculateDelay_ReturnsExpectedBackoff(int failures, int baseMs, int expectedMs)
    {
        var method = typeof(SlidePollingService)
            .GetMethod("CalculateDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var result = (TimeSpan)method.Invoke(null, [failures, baseMs])!;

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), result);
    }

    // --- Test doubles ---

    private sealed class FakeProPresenterClient : IProPresenterClient
    {
        private readonly Queue<SlideStatus>? _queue;
        private readonly SlideStatus? _fixed;

        public FakeProPresenterClient(SlideStatus status) => _fixed = status;
        public FakeProPresenterClient(Queue<SlideStatus> queue) => _queue = queue;

        public Task<string?> GetVersionAsync(CancellationToken ct = default) =>
            Task.FromResult<string?>("ProPresenter");

        public Task<string> GetRawSlideJsonAsync(CancellationToken ct = default) =>
            Task.FromResult("{}");

        public Task<byte[]?> GetCurrentSlideImageAsync(int quality, CancellationToken ct = default) =>
            Task.FromResult<byte[]?>(null);

        public Task<SlideStatus> GetCurrentSlideAsync(CancellationToken ct = default)
        {
            if (_queue is { Count: > 0 })
                return Task.FromResult(_queue.Dequeue());

            return Task.FromResult(_fixed
                ?? new SlideStatus(null, null, ConnectionState.Unavailable, DateTimeOffset.UtcNow));
        }
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<ProPresenterOptions>
    {
        public StaticOptionsMonitor(ProPresenterOptions value) => CurrentValue = value;
        public ProPresenterOptions CurrentValue { get; }
        public ProPresenterOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ProPresenterOptions, string?> listener) => null;
    }
}
