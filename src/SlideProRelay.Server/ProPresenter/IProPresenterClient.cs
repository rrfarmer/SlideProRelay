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
}
