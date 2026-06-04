using ProSlideRelay.Server.ProPresenter.Models;

namespace ProSlideRelay.Server.ProPresenter;

public interface IProPresenterClient
{
    Task<string?> GetVersionAsync(CancellationToken ct = default);
    Task<SlideStatus> GetCurrentSlideAsync(CancellationToken ct = default);
}
