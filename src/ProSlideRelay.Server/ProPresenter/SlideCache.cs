using ProSlideRelay.Server.ProPresenter.Models;

namespace ProSlideRelay.Server.ProPresenter;

public sealed class SlideCache
{
    private volatile SlideStatus? _latest;

    public SlideStatus? Latest => _latest;

    public void Update(SlideStatus status) => _latest = status;
}
