using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SlideProRelay.Server.ProPresenter.Api;
using SlideProRelay.Server.ProPresenter.Models;

namespace SlideProRelay.Server.ProPresenter;

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
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync("/v1/status/slide", ct);
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            // Genuine transport failure (refused, timeout, DNS) — ProPresenter is unreachable.
            _logger.LogDebug("ProPresenter unreachable: {Message}", ex.Message);
            return new SlideStatus(null, null, ConnectionState.Unavailable, DateTimeOffset.UtcNow);
        }

        using (response)
        {
            // We got an HTTP reply, so ProPresenter IS reachable. When nothing is
            // live — e.g. after "Clear All" — ProPresenter may return a non-success
            // status or an empty/odd body. That is "connected, nothing to show",
            // NOT a disconnection. Only the transport failures above count as offline.
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "ProPresenter reachable but returned {Status} (nothing live / cleared)",
                    (int)response.StatusCode);
                return new SlideStatus(null, null, ConnectionState.Connected, DateTimeOffset.UtcNow);
            }

            try
            {
                var body = await response.Content.ReadFromJsonAsync<ProPresenterSlideResponse>(JsonOptions, ct);
                return new SlideStatus(
                    ToSlideInfo(body?.Current),
                    ToSlideInfo(body?.Next),
                    ConnectionState.Connected,
                    DateTimeOffset.UtcNow);
            }
            catch (JsonException ex)
            {
                // Reachable but the body wasn't valid slide JSON (empty/odd response
                // when cleared). Still connected — just no content to display.
                _logger.LogDebug("ProPresenter slide body not parseable (likely cleared): {Message}", ex.Message);
                return new SlideStatus(null, null, ConnectionState.Connected, DateTimeOffset.UtcNow);
            }
        }
    }

    public async Task<byte[]?> GetCurrentSlideImageAsync(int quality, CancellationToken ct = default)
    {
        try
        {
            // 1. Find where we are: the active presentation's uuid + current cue index.
            //    (ProPresenter has no slide-uuid → image endpoint; thumbnails are keyed
            //     by presentation uuid + cue index.)
            using var idxResp = await _http.GetAsync("/v1/presentation/slide_index", ct);
            if (!idxResp.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(await idxResp.Content.ReadAsStringAsync(ct));
            // ProPresenter 21.x returns this under "presentation_index"; the community
            // spec calls it "presentation". Accept either for safety.
            if ((!doc.RootElement.TryGetProperty("presentation_index", out var pres) &&
                 !doc.RootElement.TryGetProperty("presentation", out pres)) ||
                pres.ValueKind != JsonValueKind.Object)
                return null; // nothing active

            if (!pres.TryGetProperty("index", out var indexEl) ||
                !pres.TryGetProperty("presentation_id", out var pid) ||
                !pid.TryGetProperty("uuid", out var uuidEl))
                return null;

            var index = indexEl.GetInt32();
            var uuid = uuidEl.GetString();
            if (string.IsNullOrEmpty(uuid) || index < 0)
                return null;

            // 2. Fetch the thumbnail at the requested quality (pixels, longest edge).
            using var imgReq = new HttpRequestMessage(
                HttpMethod.Get, $"/v1/presentation/{uuid}/thumbnail/{index}?quality={quality}");
            imgReq.Headers.Accept.ParseAdd("image/jpeg");
            using var imgResp = await _http.SendAsync(imgReq, ct);
            if (!imgResp.IsSuccessStatusCode)
                return null;

            var bytes = await imgResp.Content.ReadAsByteArrayAsync(ct);
            return bytes.Length > 0 ? bytes : null;
        }
        catch (Exception ex) when (IsConnectionFailure(ex) || ex is JsonException)
        {
            _logger.LogDebug("Slide image unavailable: {Message}", ex.Message);
            return null;
        }
    }

    private static SlideInfo? ToSlideInfo(ProPresenterSlideEntry? entry)
    {
        if (entry is null) return null;
        return new SlideInfo(entry.Uuid, entry.Text, entry.Notes);
    }

    public async Task<string> GetRawSlideJsonAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetStringAsync("/v1/status/slide", ct);
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            return $"{{\"error\":\"unavailable\",\"message\":{JsonSerializer.Serialize(ex.Message)}}}";
        }
    }

    // A transport-level failure means ProPresenter is genuinely unreachable.
    // (A bad/empty HTTP body is handled at the call site, not here — that's a
    // reachable server with nothing to show, not a disconnection.)
    private static bool IsConnectionFailure(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or OperationCanceledException;
}
