namespace SlideProRelay.Server.ProPresenter;

/// <summary>No-op detector for platforms without an implementation.</summary>
public sealed class NullProPresenterPortDetector : IProPresenterPortDetector
{
    public int? TryDetectNetworkPort() => null;
}
