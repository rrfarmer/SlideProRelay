namespace SlideProRelay.Server.ProPresenter;

/// <summary>
/// Discovers the network/API port ProPresenter is currently listening on by
/// reading ProPresenter's own persisted preferences. Only meaningful when the
/// relay runs on the same machine as ProPresenter (the supported setup).
/// Returns null when the port can't be determined (network disabled, prefs
/// missing, unsupported OS, or ProPresenter not installed).
/// </summary>
public interface IProPresenterPortDetector
{
    int? TryDetectNetworkPort();
}
