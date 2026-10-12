namespace SlideProRelay.Server.ScreenCapture;

public sealed class ScreenCaptureOptions
{
    public const string SectionName = "ScreenCapture";

    /// <summary>
    /// When true, the relay grabs a full-screen JPEG of the output display each
    /// time the live slide changes and caches it for <c>/api/screen-capture</c>.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 1-based display to capture (matches macOS <c>screencapture -D</c>).
    /// 0 = auto: capture the first non-primary display, preferring the one whose
    /// size matches ProPresenter's audience screen. Auto never captures the
    /// primary display. On a single-display machine nothing is captured until a
    /// display is chosen here explicitly, so the operator's desktop is never
    /// published by accident.
    /// </summary>
    public int DisplayIndex { get; init; } = 0;
}
