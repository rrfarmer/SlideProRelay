namespace SlideProRelay.Server.ScreenCapture;

/// <summary>
/// Decides which display to capture, combining the user's manual override with a
/// best-effort auto-match against ProPresenter's audience-screen resolution.
/// </summary>
public static class DisplaySelector
{
    /// <summary>
    /// Resolves the 1-based display index to capture:
    /// <list type="bullet">
    /// <item>A manual <paramref name="configuredIndex"/> (≥1, in range) always wins.</item>
    /// <item>Otherwise prefer the single non-primary display whose size matches the
    /// audience screen; if that's ambiguous (0 or &gt;1 matches), fall back to the
    /// first non-primary display, then the primary.</item>
    /// </list>
    /// Returns <paramref name="configuredIndex"/> unchanged when the display list is
    /// empty, leaving the capture service to apply its own default.
    /// </summary>
    public static int Resolve(
        IReadOnlyList<CaptureDisplay> displays,
        int configuredIndex,
        int? audienceWidth,
        int? audienceHeight)
    {
        if (displays.Count == 0)
            return configuredIndex;

        // Manual override wins, as long as it points at a real display.
        if (configuredIndex >= 1 && configuredIndex <= displays.Count)
            return configuredIndex;

        var nonPrimary = displays.Where(d => !d.IsPrimary).ToList();

        // Auto-match the audience resolution when it's unambiguous.
        if (audienceWidth is int aw && audienceHeight is int ah)
        {
            var matches = nonPrimary.Where(d => d.Width == aw && d.Height == ah).ToList();
            if (matches.Count == 1)
                return matches[0].Index;
        }

        if (nonPrimary.Count > 0)
            return nonPrimary[0].Index;

        return displays[0].Index;
    }
}
