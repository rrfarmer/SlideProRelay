using SlideProRelay.Server.ProPresenter;

namespace SlideProRelay.Tests.ProPresenter;

public sealed class SlideHistoryTests
{
    [Fact]
    public void SeenCount_IsZero_ForUnseenSlide()
    {
        var history = new SlideHistory();
        Assert.Equal(0, history.SeenCount("uuid-1"));
    }

    [Fact]
    public void Observe_CountsFirstAppearanceOnce()
    {
        var history = new SlideHistory();
        history.Observe("uuid-1");
        Assert.Equal(1, history.SeenCount("uuid-1"));
    }

    [Fact]
    public void Observe_SameSlideRepeatedly_CountsOnlyOnce()
    {
        // The live slide stays the same across many polls — that's one appearance.
        var history = new SlideHistory();
        history.Observe("uuid-1");
        history.Observe("uuid-1");
        history.Observe("uuid-1");
        Assert.Equal(1, history.SeenCount("uuid-1"));
    }

    [Fact]
    public void Observe_CountsRepeat_WhenSlideReturnsAfterAnother()
    {
        // uuid-1 → uuid-2 → uuid-1 means uuid-1 has now been shown twice.
        var history = new SlideHistory();
        history.Observe("uuid-1");
        history.Observe("uuid-2");
        history.Observe("uuid-1");

        Assert.Equal(2, history.SeenCount("uuid-1"));
        Assert.Equal(1, history.SeenCount("uuid-2"));
    }

    [Fact]
    public void Observe_IgnoresNullOrEmpty()
    {
        var history = new SlideHistory();
        history.Observe(null);
        history.Observe("");
        Assert.Equal(0, history.SeenCount(null));
        Assert.Equal(0, history.SeenCount(""));
    }

    [Fact]
    public void Observe_NullBetweenSameSlide_DoesNotDoubleCount()
    {
        // A blank/cleared poll (null) between two polls of the same live slide
        // must not be treated as the slide "returning".
        var history = new SlideHistory();
        history.Observe("uuid-1");
        history.Observe(null);
        history.Observe("uuid-1");
        Assert.Equal(1, history.SeenCount("uuid-1"));
    }
}
