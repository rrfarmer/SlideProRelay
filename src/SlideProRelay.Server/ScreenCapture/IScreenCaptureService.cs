namespace SlideProRelay.Server.ScreenCapture;

/// <summary>
/// Grabs a full-screen JPEG of a physical output display — the real pixels the
/// projector/screen shows, not ProPresenter's rendered thumbnail. Platform
/// implementations are selected at runtime (macOS now, Windows DXGI next).
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>True when this platform actually has a working capture path.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Captures the configured/auto-selected output display as a JPEG. Returns
    /// null when capture is unsupported or fails (e.g. Screen Recording
    /// permission not yet granted on macOS).
    /// </summary>
    Task<byte[]?> CaptureAsync(CancellationToken ct = default);
}
