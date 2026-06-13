using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SlideProRelay.Server.SlidePro;

/// <summary>
/// Thin wrapper around the three SlidePro public API calls.
/// Registered as a singleton; creates named HttpClients via IHttpClientFactory.
/// The API key is passed per-call so it can be updated at runtime without restarting.
/// </summary>
public sealed class SlideProApiClient(
    IHttpClientFactory httpFactory,
    IOptionsMonitor<SlideProOptions> opts,
    ILogger<SlideProApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<PresentationDto>?> GetPresentationsAsync(string apiKey, CancellationToken ct = default)
    {
        var baseUrl = opts.CurrentValue.BaseUrl.TrimEnd('/');
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/presentations");
        req.Headers.Add("X-Api-Key", apiKey);

        try
        {
            using var http = httpFactory.CreateClient("SlidePro");
            var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<PresentationDto>>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SlidePro: failed to fetch presentations");
            return null;
        }
    }

    public async Task<UploadSlideResponseDto?> UploadSlideAsync(
        string apiKey, string presentationId, byte[] jpeg, bool presented, CancellationToken ct = default)
    {
        var baseUrl = opts.CurrentValue.BaseUrl.TrimEnd('/');

        var content = new ByteArrayContent(jpeg);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        using var form = new MultipartFormDataContent();
        form.Add(content, "file", "slide.jpg");
        form.Add(new StringContent(presentationId), "presentationId");
        form.Add(new StringContent(presented ? "true" : "false"), "presented");

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/slides/upload") { Content = form };
        req.Headers.Add("X-Api-Key", apiKey);

        try
        {
            using var http = httpFactory.CreateClient("SlidePro");
            var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<UploadSlideResponseDto>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SlidePro: failed to upload slide");
            return null;
        }
    }

    public async Task<RepresentSlideResponseDto?> RepresentSlideAsync(
        string apiKey, string presentationId, int slideId, CancellationToken ct = default)
    {
        var baseUrl = opts.CurrentValue.BaseUrl.TrimEnd('/');
        var body = JsonSerializer.Serialize(new { presentationId, slideId });

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/slides/represent")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Api-Key", apiKey);

        try
        {
            using var http = httpFactory.CreateClient("SlidePro");
            var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<RepresentSlideResponseDto>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SlidePro: failed to re-present slide {SlideId}", slideId);
            return null;
        }
    }
}
