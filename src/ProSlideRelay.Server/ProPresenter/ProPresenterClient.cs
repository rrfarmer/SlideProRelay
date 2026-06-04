using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProSlideRelay.Server.ProPresenter.Api;
using ProSlideRelay.Server.ProPresenter.Models;

namespace ProSlideRelay.Server.ProPresenter;

public sealed class ProPresenterClient : IProPresenterClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<ProPresenterClient> _logger;

    public ProPresenterClient(HttpClient http, ILogger<ProPresenterClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string?> GetVersionAsync(CancellationToken ct = default)
    {
        try
        {
            var doc = await _http.GetFromJsonAsync<JsonDocument>("/version", JsonOptions, ct);
            return doc?.RootElement.GetProperty("name").GetString();
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            _logger.LogDebug("ProPresenter unreachable on version check: {Message}", ex.Message);
            return null;
        }
    }

    public async Task<SlideStatus> GetCurrentSlideAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ProPresenterSlideResponse>(
                "/v1/status/slide", JsonOptions, ct);

            var current = ToSlideInfo(response?.Current);
            var next = ToSlideInfo(response?.Next);

            return new SlideStatus(current, next, ConnectionState.Connected, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            _logger.LogDebug("ProPresenter unreachable: {Message}", ex.Message);
            return new SlideStatus(null, null, ConnectionState.Unavailable, DateTimeOffset.UtcNow);
        }
    }

    private static SlideInfo? ToSlideInfo(ProPresenterSlideEntry? entry)
    {
        if (entry is null) return null;
        return new SlideInfo(entry.Uuid, entry.Text, entry.Notes);
    }

    private static bool IsConnectionFailure(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException;
}
