using SlideProRelay.Server.ScreenCapture;

namespace SlideProRelay.Tests.ScreenCapture;

public sealed class DisplaySelectorTests
{
    private static CaptureDisplay Display(int index, int w, int h, bool primary) =>
        new(index, w, h, 0, 0, primary);

    // Primary 2560x1440 + two 1920x1080 secondaries: the real ambiguous setup.
    private static IReadOnlyList<CaptureDisplay> ThreeDisplays() =>
    [
        Display(1, 2560, 1440, primary: true),
        Display(2, 1920, 1080, primary: false),
        Display(3, 1920, 1080, primary: false),
    ];

    // A laptop with nothing plugged in: the setup where auto must NOT capture,
    // because the only display is the operator's desktop.
    private static IReadOnlyList<CaptureDisplay> PrimaryOnly() =>
        [Display(1, 1920, 1080, primary: true)];

    [Fact]
    public void ManualIndex_AlwaysWins_EvenOverAudienceMatch()
    {
        // Manual index 1 (the primary) is honored despite audience matching 2 & 3.
        var chosen = DisplaySelector.Resolve(ThreeDisplays(), configuredIndex: 1, 1920, 1080);
        Assert.Equal(1, chosen.Index);
        Assert.Equal(DisplaySelectionReason.Configured, chosen.Reason);
    }

    [Fact]
    public void ManualIndex_OutOfRange_FallsBackToAuto()
    {
        var chosen = DisplaySelector.Resolve(ThreeDisplays(), configuredIndex: 9, audienceWidth: null, audienceHeight: null);
        Assert.Equal(2, chosen.Index); // first non-primary
        Assert.Equal(DisplaySelectionReason.FirstNonPrimary, chosen.Reason);
    }

    [Fact]
    public void Auto_UniqueAudienceMatch_IsChosen()
    {
        // Primary + one 1080p + one 720p; audience 1280x720 -> the 720p display.
        IReadOnlyList<CaptureDisplay> displays =
        [
            Display(1, 2560, 1440, primary: true),
            Display(2, 1920, 1080, primary: false),
            Display(3, 1280, 720, primary: false),
        ];
        var chosen = DisplaySelector.Resolve(displays, configuredIndex: 0, 1280, 720);
        Assert.Equal(3, chosen.Index);
        Assert.Equal(DisplaySelectionReason.AudienceMatch, chosen.Reason);
        Assert.Contains("Display 3", chosen.Description);
    }

    [Fact]
    public void Auto_AmbiguousAudienceMatch_FallsBackToFirstNonPrimary()
    {
        // Two 1080p secondaries both match -> can't disambiguate, pick first.
        var chosen = DisplaySelector.Resolve(ThreeDisplays(), configuredIndex: 0, 1920, 1080);
        Assert.Equal(2, chosen.Index);
        Assert.Equal(DisplaySelectionReason.FirstNonPrimary, chosen.Reason);
    }

    [Fact]
    public void Auto_NoAudienceInfo_UsesFirstNonPrimary()
    {
        var chosen = DisplaySelector.Resolve(ThreeDisplays(), configuredIndex: 0, null, null);
        Assert.Equal(2, chosen.Index);
        Assert.Equal(DisplaySelectionReason.FirstNonPrimary, chosen.Reason);
    }

    // ── Single display ────────────────────────────────────────────────────────

    [Fact]
    public void Auto_OnlyPrimary_CapturesNothing()
    {
        var chosen = DisplaySelector.Resolve(PrimaryOnly(), configuredIndex: 0, null, null);
        Assert.False(chosen.HasDisplay);
        Assert.Equal(0, chosen.Index);
        Assert.Equal(DisplaySelectionReason.NoSecondaryDisplay, chosen.Reason);
    }

    [Fact]
    public void Auto_OnlyPrimary_StaysOff_EvenWhenAudienceMatchesPrimary()
    {
        // ProPresenter's audience output may well BE the primary (windowed output
        // on a laptop). Auto still refuses: the primary is the operator's desktop.
        var chosen = DisplaySelector.Resolve(PrimaryOnly(), configuredIndex: 0, 1920, 1080);
        Assert.False(chosen.HasDisplay);
        Assert.Equal(DisplaySelectionReason.NoSecondaryDisplay, chosen.Reason);
    }

    [Fact]
    public void ManualIndex_OnlyPrimary_CapturesPrimary()
    {
        // Capturing the primary is allowed, but only as an explicit choice.
        var chosen = DisplaySelector.Resolve(PrimaryOnly(), configuredIndex: 1, 1920, 1080);
        Assert.True(chosen.HasDisplay);
        Assert.Equal(1, chosen.Index);
        Assert.Equal(DisplaySelectionReason.Configured, chosen.Reason);
    }

    [Fact]
    public void ManualIndex_OutOfRange_OnlyPrimary_CapturesNothing()
    {
        // A stale index (the second display was unplugged) must not silently
        // degrade to capturing the desktop.
        var chosen = DisplaySelector.Resolve(PrimaryOnly(), configuredIndex: 2, null, null);
        Assert.False(chosen.HasDisplay);
        Assert.Equal(DisplaySelectionReason.NoSecondaryDisplay, chosen.Reason);
    }

    [Fact]
    public void Description_OnlyPrimary_TellsOperatorToPickOne()
    {
        var chosen = DisplaySelector.Resolve(PrimaryOnly(), configuredIndex: 0, null, null);
        Assert.Equal("No second display; pick one to capture", chosen.Description);
    }

    // ── No displays enumerated ────────────────────────────────────────────────

    [Fact]
    public void NoDisplays_Auto_CapturesNothing()
    {
        var chosen = DisplaySelector.Resolve([], configuredIndex: 0, 1920, 1080);
        Assert.False(chosen.HasDisplay);
        Assert.Equal(0, chosen.Index);
        Assert.Equal(DisplaySelectionReason.NoDisplays, chosen.Reason);
    }

    [Fact]
    public void NoDisplays_ManualIndex_PassesThrough()
    {
        // Enumeration can fail while capture still works; trust the operator.
        var chosen = DisplaySelector.Resolve([], configuredIndex: 2, null, null);
        Assert.Equal(2, chosen.Index);
        Assert.Equal(DisplaySelectionReason.Configured, chosen.Reason);
    }
}
