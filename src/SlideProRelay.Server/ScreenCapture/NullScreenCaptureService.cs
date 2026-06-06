namespace SlideProRelay.Server.ScreenCapture;

/// <summary>
/// Used on platforms without a capture implementation yet — Windows until the
/// DXGI Desktop Duplication path lands, and Linux. Never produces a frame.
/// </summary>
public sealed class NullScreenCaptureService : IScreenCaptureService
{
    public bool IsSupported => false;

    public Task<byte[]?> CaptureAsync(CancellationToken ct = default) =>
        Task.FromResult<byte[]?>(null);
}
