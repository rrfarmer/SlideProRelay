using Microsoft.Extensions.Options;
using ProSlideRelay.Server.ProPresenter.Models;

namespace ProSlideRelay.Server.ProPresenter;

public sealed class SlidePollingService : BackgroundService
{
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

        ConnectionState? lastReported = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var status = await _client.GetCurrentSlideAsync(ct);
                _cache.Update(status);

                if (status.Connection != lastReported)
                {
                    if (status.Connection == ConnectionState.Connected)
                        _logger.LogInformation("Connected to ProPresenter");
                    else
                        _logger.LogWarning("ProPresenter unavailable — waiting to reconnect");

                    lastReported = status.Connection;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error in slide polling");
            }

            var interval = _opts.CurrentValue.PollingIntervalMs;
            await Task.Delay(interval, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        _logger.LogInformation("Slide polling stopped");
    }
}
