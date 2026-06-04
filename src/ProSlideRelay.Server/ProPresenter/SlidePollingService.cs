using Microsoft.Extensions.Options;
using ProSlideRelay.Server.ProPresenter.Models;

namespace ProSlideRelay.Server.ProPresenter;

public sealed class SlidePollingService : BackgroundService
{
    // Backoff caps at 30 s so a long outage doesn't go completely silent
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly IProPresenterClient _client;
    private readonly SlideCache _cache;
    private readonly IOptionsMonitor<ProPresenterOptions> _opts;
    private readonly ILogger<SlidePollingService> _logger;

    public SlidePollingService(
        IProPresenterClient client,
        SlideCache cache,
        IOptionsMonitor<ProPresenterOptions> opts,
        ILogger<SlidePollingService> logger)
    {
        _client = client;
        _cache = cache;
        _opts = opts;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Slide polling started");

        ConnectionState? lastState = null;
        int consecutiveFailures = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var status = await _client.GetCurrentSlideAsync(ct);
                _cache.Update(status);

                if (status.Connection == ConnectionState.Connected)
                {
                    if (lastState != ConnectionState.Connected)
                        _logger.LogInformation("Connected to ProPresenter");

                    consecutiveFailures = 0;
                }
                else
                {
                    consecutiveFailures++;
                    LogUnavailable(consecutiveFailures);
                }

                lastState = status.Connection;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                consecutiveFailures++;
                _logger.LogError(ex, "Unexpected error in slide polling (failure #{Count})", consecutiveFailures);
            }

            var delay = CalculateDelay(consecutiveFailures, _opts.CurrentValue.PollingIntervalMs);
            await Task.Delay(delay, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        _logger.LogInformation("Slide polling stopped");
    }

    private void LogUnavailable(int consecutiveFailures)
    {
        // Log on 1st failure, then only every ~10 attempts to avoid spam
        if (consecutiveFailures == 1)
            _logger.LogWarning("ProPresenter unavailable — retrying with backoff");
        else if (consecutiveFailures % 10 == 0)
            _logger.LogDebug("ProPresenter still unavailable (attempt {Count})", consecutiveFailures);
    }

    private static TimeSpan CalculateDelay(int failures, int baseIntervalMs)
    {
        if (failures <= 1)
            return TimeSpan.FromMilliseconds(baseIntervalMs);

        // Double the interval for each failure, up to MaxBackoff
        var backoffMs = baseIntervalMs * Math.Pow(2, failures - 1);
        var capped = Math.Min(backoffMs, MaxBackoff.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(capped);
    }
}
