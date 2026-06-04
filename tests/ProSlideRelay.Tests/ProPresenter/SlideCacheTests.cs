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

    [Fact]
    public void Subscribe_HandlerIsCalledOnUpdate()
    {
        var cache = new SlideCache();
        SlideStatus? received = null;
        var status = new SlideStatus(new SlideInfo("u1", "Hello", null), null, ConnectionState.Connected, DateTimeOffset.UtcNow);

        using (cache.Subscribe(s => received = s))
            cache.Update(status);

        Assert.Same(status, received);
    }

    [Fact]
    public void Subscribe_DisposedHandlerIsNotCalled()
    {
        var cache = new SlideCache();
        var callCount = 0;

        var sub = cache.Subscribe(_ => callCount++);
        sub.Dispose();
        cache.Update(new SlideStatus(null, null, ConnectionState.Unavailable, DateTimeOffset.UtcNow));

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Subscribe_MultipleHandlersAllReceiveUpdate()
    {
        var cache = new SlideCache();
        var received = new List<string?>();
        var status = new SlideStatus(new SlideInfo("u1", "Broadcast text", null), null, ConnectionState.Connected, DateTimeOffset.UtcNow);

        using (cache.Subscribe(s => received.Add("A:" + s.Current?.Text)))
        using (cache.Subscribe(s => received.Add("B:" + s.Current?.Text)))
            cache.Update(status);

        Assert.Contains("A:Broadcast text", received);
        Assert.Contains("B:Broadcast text", received);
    }
}
