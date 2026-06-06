namespace SlideProRelay.Server.ScreenCapture;

/// <summary>
/// Grabs a full-screen JPEG of a physical output display — the real pixels the
/// projector/screen shows, not ProPresenter's rendered thumbnail. Platform
/// implementations are selected at runtime (macOS + Windows DXGI).
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>True when this platform actually has a working capture path.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Enumerates the capturable displays (1-based <see cref="CaptureDisplay.Index"/>),
    /// so callers can pick one and so the tray UIs can present a chooser. Empty
    /// when unsupported or enumeration fails.
    /// </summary>
    IReadOnlyList<CaptureDisplay> GetDisplays();

    /// <summary>
    /// Captures the given 1-based display as a JPEG. Pass <c>&lt;= 0</c> to let the
    /// implementation pick its default (first non-primary display). Returns null
    /// when capture is unsupported or fails (e.g. Screen Recording permission not
    /// yet granted on macOS).
    /// </summary>
    Task<byte[]?> CaptureAsync(int displayIndex, CancellationToken ct = default);
}
