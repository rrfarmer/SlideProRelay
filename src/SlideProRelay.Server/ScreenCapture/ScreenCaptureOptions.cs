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
    /// 0 = auto: capture the first non-primary display when more than one is
    /// attached (typically the ProPresenter output), otherwise the main display.
    /// </summary>
    public int DisplayIndex { get; init; } = 0;
}
