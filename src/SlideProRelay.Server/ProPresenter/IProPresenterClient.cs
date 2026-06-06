using SlideProRelay.Server.ProPresenter.Models;

namespace SlideProRelay.Server.ProPresenter;

public interface IProPresenterClient
{
    Task<string?> GetVersionAsync(CancellationToken ct = default);
    Task<SlideStatus> GetCurrentSlideAsync(CancellationToken ct = default);
    Task<string> GetRawSlideJsonAsync(CancellationToken ct = default);

    /// <summary>
    /// EXPERIMENTAL: fetches a JPEG thumbnail of the current slide/cue at the
    /// requested pixel size (longest edge). Returns null when nothing is live or
    /// ProPresenter is unreachable. image/jpeg.
    /// </summary>
    Task<byte[]?> GetCurrentSlideImageAsync(int quality, CancellationToken ct = default);

    /// <summary>
    /// Returns the resolution of ProPresenter's audience output screen (from
    /// /v1/status/screens), used to auto-match which physical display to capture.
    /// Null when unreachable or no audience screen is configured.
    /// </summary>
    Task<(int Width, int Height)?> GetAudienceScreenSizeAsync(CancellationToken ct = default);
}
