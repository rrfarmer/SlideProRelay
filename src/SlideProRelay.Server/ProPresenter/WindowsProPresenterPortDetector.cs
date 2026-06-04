using System.Runtime.Versioning;

namespace SlideProRelay.Server.ProPresenter;

/// <summary>
/// Windows port detection. ProPresenter on Windows also assigns and persists its
/// network port automatically (Preferences → Network), so this is implementable —
/// it just needs the on-disk location confirmed on a real Windows machine.
///
/// TODO(windows): ProPresenter stores preferences under the user profile
/// (likely C:\Users\&lt;user&gt;\AppData\Local\RenewedVision\ProPresenter\ or
/// %ProgramData%\RenewedVision\ProPresenter\). Locate the file that holds
/// "networkPort"/"networkEnabled" and parse it here. Until then this returns
/// null, so the relay falls back to the configured port — no worse than before.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsProPresenterPortDetector : IProPresenterPortDetector
{
    private readonly ILogger<WindowsProPresenterPortDetector> _logger;

    public WindowsProPresenterPortDetector(ILogger<WindowsProPresenterPortDetector> logger) => _logger = logger;

    public int? TryDetectNetworkPort()
    {
        _logger.LogDebug("Windows ProPresenter port auto-detection is not yet implemented; using configured port.");
        return null;
    }
}
