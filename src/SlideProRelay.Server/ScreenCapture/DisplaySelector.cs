namespace SlideProRelay.Server.ScreenCapture;

/// <summary>Why <see cref="DisplaySelector"/> picked a display, or declined to.</summary>
public enum DisplaySelectionReason
{
    /// <summary>A manual <c>ScreenCapture:DisplayIndex</c> was honored.</summary>
    Configured,

    /// <summary>Exactly one non-primary display matches ProPresenter's audience-screen size.</summary>
    AudienceMatch,

    /// <summary>No unique audience match, so the first non-primary display was picked.</summary>
    FirstNonPrimary,

    /// <summary>
    /// Only the primary display is attached. Auto never captures it (that would
    /// publish the operator's desktop), so nothing is captured until the operator
    /// picks a display explicitly.
    /// </summary>
    NoSecondaryDisplay,

    /// <summary>Display enumeration returned nothing, so there is nothing to auto-pick.</summary>
    NoDisplays,
}

/// <summary>
/// Outcome of <see cref="DisplaySelector.Resolve"/>: the 1-based display to
/// capture, or <see cref="Index"/> 0 when nothing should be captured.
/// </summary>
public sealed record DisplaySelection(int Index, DisplaySelectionReason Reason)
{
    /// <summary>True when a display was picked; false means "capture nothing".</summary>
    public bool HasDisplay => Index >= 1;

    /// <summary>
    /// Operator-facing explanation, surfaced by <c>/api/displays</c>, the settings
    /// page and the coordinator's log.
    /// </summary>
    public string Description => Reason switch
    {
        DisplaySelectionReason.Configured => $"Display {Index} (chosen in settings)",
        DisplaySelectionReason.AudienceMatch => $"Display {Index} matches ProPresenter's audience screen",
        DisplaySelectionReason.FirstNonPrimary => $"Display {Index} (first non-primary display)",
        DisplaySelectionReason.NoSecondaryDisplay => "No second display; pick one to capture",
        DisplaySelectionReason.NoDisplays => "No displays detected",
        _ => Reason.ToString(),
    };
}

/// <summary>
/// Decides which display to capture, combining the user's manual override with a
/// best-effort auto-match against ProPresenter's audience-screen resolution.
/// </summary>
public static class DisplaySelector
{
    /// <summary>
    /// Resolves which 1-based display to capture:
    /// <list type="bullet">
    /// <item>A manual <paramref name="configuredIndex"/> (>=1, in range) always wins.
    /// It is also passed through untouched when no displays could be enumerated,
    /// since enumeration can fail even though capture works.</item>
    /// <item>Otherwise prefer the single non-primary display whose size matches the
    /// audience screen; if that's ambiguous (0 or more than 1 match), fall back to
    /// the first non-primary display.</item>
    /// <item>Auto never picks the primary display. With only the primary attached
    /// (or nothing enumerated) the result has <see cref="DisplaySelection.Index"/> 0:
    /// capture nothing. Capturing the primary, i.e. the operator's desktop, must be
    /// an explicit choice.</item>
    /// </list>
    /// </summary>
    public static DisplaySelection Resolve(
        IReadOnlyList<CaptureDisplay> displays,
        int configuredIndex,
        int? audienceWidth,
        int? audienceHeight)
    {
        if (displays.Count == 0)
        {
            return configuredIndex >= 1
                ? new(configuredIndex, DisplaySelectionReason.Configured)
                : new(0, DisplaySelectionReason.NoDisplays);
        }

        // Manual override wins, as long as it points at a real display.
        if (configuredIndex >= 1 && configuredIndex <= displays.Count)
            return new(configuredIndex, DisplaySelectionReason.Configured);

        var nonPrimary = displays.Where(d => !d.IsPrimary).ToList();
        if (nonPrimary.Count == 0)
            return new(0, DisplaySelectionReason.NoSecondaryDisplay);

        // Auto-match the audience resolution when it's unambiguous.
        if (audienceWidth is int aw && audienceHeight is int ah)
        {
            var matches = nonPrimary.Where(d => d.Width == aw && d.Height == ah).ToList();
            if (matches.Count == 1)
                return new(matches[0].Index, DisplaySelectionReason.AudienceMatch);
        }

        return new(nonPrimary[0].Index, DisplaySelectionReason.FirstNonPrimary);
    }
}
