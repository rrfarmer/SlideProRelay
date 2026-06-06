using SlideProRelay.Server.ScreenCapture;

namespace SlideProRelay.Tests.ScreenCapture;

public sealed class DisplaySelectorTests
{
    private static CaptureDisplay Display(int index, int w, int h, bool primary) =>
        new(index, w, h, 0, 0, primary);

    // Primary 2560x1440 + two 1920x1080 secondaries — the real ambiguous setup.
    private static IReadOnlyList<CaptureDisplay> ThreeDisplays() =>
    [
        Display(1, 2560, 1440, primary: true),
        Display(2, 1920, 1080, primary: false),
        Display(3, 1920, 1080, primary: false),
    ];

    [Fact]
    public void ManualIndex_AlwaysWins_EvenOverAudienceMatch()
    {
        // Manual index 1 (the primary) is honored despite audience matching 2 & 3.
        var chosen = DisplaySelector.Resolve(ThreeDisplays(), configuredIndex: 1, 1920, 1080);
        Assert.Equal(1, chosen);
    }

    [Fact]
    public void ManualIndex_OutOfRange_FallsBackToAuto()
    {
        var chosen = DisplaySelector.Resolve(ThreeDisplays(), configuredIndex: 9, audienceWidth: null, audienceHeight: null);
        Assert.Equal(2, chosen); // first non-primary
    }

    [Fact]
    public void Auto_UniqueAudienceMatch_IsChosen()
    {
        // Primary + one 1080p + one 720p; audience 1280x720 → the 720p display.
        IReadOnlyList<CaptureDisplay> displays =
        [
            Display(1, 2560, 1440, primary: true),
            Display(2, 1920, 1080, primary: false),
            Display(3, 1280, 720, primary: false),
        ];
        var chosen = DisplaySelector.Resolve(displays, configuredIndex: 0, 1280, 720);
        Assert.Equal(3, chosen);
    }

    [Fact]
    public void Auto_AmbiguousAudienceMatch_FallsBackToFirstNonPrimary()
    {
        // Two 1080p secondaries both match → can't disambiguate, pick first.
        var chosen = DisplaySelector.Resolve(ThreeDisplays(), configuredIndex: 0, 1920, 1080);
        Assert.Equal(2, chosen);
    }

    [Fact]
    public void Auto_NoAudienceInfo_UsesFirstNonPrimary()
    {
        var chosen = DisplaySelector.Resolve(ThreeDisplays(), configuredIndex: 0, null, null);
        Assert.Equal(2, chosen);
    }

    [Fact]
    public void Auto_OnlyPrimary_UsesPrimary()
    {
        IReadOnlyList<CaptureDisplay> displays = [Display(1, 2560, 1440, primary: true)];
        var chosen = DisplaySelector.Resolve(displays, configuredIndex: 0, 1920, 1080);
        Assert.Equal(1, chosen);
    }

    [Fact]
    public void NoDisplays_ReturnsConfiguredIndex()
    {
        var chosen = DisplaySelector.Resolve([], configuredIndex: 0, 1920, 1080);
        Assert.Equal(0, chosen);
    }
}
