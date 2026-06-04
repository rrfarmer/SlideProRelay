using ProSlideRelay.Server.ProPresenter.Models;

namespace ProSlideRelay.Server.ProPresenter;

public sealed class SlideCache
{
    private volatile SlideStatus? _latest;
    private readonly List<Action<SlideStatus>> _subscribers = [];
    private readonly Lock _lock = new();

    public SlideStatus? Latest => _latest;

    public void Update(SlideStatus status)
    {
        _latest = status;

        Action<SlideStatus>[] snapshot;
        lock (_lock)
            snapshot = [.. _subscribers];

        foreach (var sub in snapshot)
            sub(status);
    }

    public IDisposable Subscribe(Action<SlideStatus> handler)
    {
        lock (_lock)
            _subscribers.Add(handler);

        return new Subscription(() =>
        {
            lock (_lock)
                _subscribers.Remove(handler);
        });
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
