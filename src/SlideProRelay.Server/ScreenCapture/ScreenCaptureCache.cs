namespace SlideProRelay.Server.ScreenCapture;

/// <summary>
/// Holds the most recently captured frame so <c>/api/screen-capture</c> can
/// serve it without re-capturing on every request. Written by
/// <see cref="ScreenCaptureCoordinator"/> on each slide change.
/// </summary>
public sealed class ScreenCaptureCache
{
    private volatile Frame? _latest;

    public Frame? Latest => _latest;

    public void Set(byte[] jpeg, string key) =>
        _latest = new Frame(jpeg, key, DateTimeOffset.UtcNow);

    /// <summary>Drops the cached frame (e.g. when nothing is live on screen).</summary>
    public void Clear() => _latest = null;

    public sealed record Frame(byte[] Jpeg, string Key, DateTimeOffset CapturedAt);
}
