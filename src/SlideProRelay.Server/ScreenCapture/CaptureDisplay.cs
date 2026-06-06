namespace SlideProRelay.Server.ScreenCapture;

/// <summary>
/// A capturable OS display. <see cref="Index"/> is 1-based and is the value used
/// for <c>ScreenCapture:DisplayIndex</c> (and macOS <c>screencapture -D</c>).
/// </summary>
public sealed record CaptureDisplay(int Index, int Width, int Height, int X, int Y, bool IsPrimary);
