namespace SlideProRelay.Server.ScreenCapture;

/// <summary>
/// Used on platforms without a capture implementation (Linux). Never produces a
/// frame and reports no displays.
/// </summary>
public sealed class NullScreenCaptureService : IScreenCaptureService
{
    public bool IsSupported => false;

    public IReadOnlyList<CaptureDisplay> GetDisplays() => [];

    public Task<byte[]?> CaptureAsync(int displayIndex, CancellationToken ct = default) =>
        Task.FromResult<byte[]?>(null);
}
