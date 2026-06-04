using ProSlideRelay.Server.ProPresenter;
using ProSlideRelay.Server.ProPresenter.Models;

namespace ProSlideRelay.Tests.ProPresenter;

public sealed class SlideCacheTests
{
    [Fact]
    public void Latest_IsNullBeforeFirstUpdate()
    {
        var cache = new SlideCache();
        Assert.Null(cache.Latest);
    }

    [Fact]
    public void Update_StoresStatus()
    {
        var cache = new SlideCache();
        var status = new SlideStatus(
            new SlideInfo("uuid-1", "Line one", null),
            null,
            ConnectionState.Connected,
            DateTimeOffset.UtcNow);

        cache.Update(status);

        Assert.Same(status, cache.Latest);
    }

    [Fact]
    public void Update_ReplacesOldStatus()
    {
        var cache = new SlideCache();
        var first = new SlideStatus(null, null, ConnectionState.Unavailable, DateTimeOffset.UtcNow);
        var second = new SlideStatus(
            new SlideInfo("uuid-2", "New slide", null),
            null,
            ConnectionState.Connected,
            DateTimeOffset.UtcNow);

        cache.Update(first);
        cache.Update(second);

        Assert.Same(second, cache.Latest);
    }
}
