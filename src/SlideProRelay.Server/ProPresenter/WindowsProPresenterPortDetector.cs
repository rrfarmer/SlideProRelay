using System.Runtime.Versioning;

namespace SlideProRelay.Server.ProPresenter;

/// <summary>
/// Reads ProPresenter's network port from its Windows preferences file
/// (%APPDATA%\RenewedVision\ProPresenter\Preferences\NetworkPreferences.proPref).
/// ProPresenter assigns this port automatically and persists it, so reading it
/// lets the relay follow the port without manual configuration.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsProPresenterPortDetector : IProPresenterPortDetector
{
    private static readonly string PrefsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RenewedVision", "ProPresenter", "Preferences", "NetworkPreferences.proPref");

    private readonly ILogger<WindowsProPresenterPortDetector> _logger;

    public WindowsProPresenterPortDetector(ILogger<WindowsProPresenterPortDetector> logger) => _logger = logger;

    public int? TryDetectNetworkPort()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            if (!File.Exists(PrefsPath))
            {
                _logger.LogDebug("ProPresenter NetworkPreferences.proPref not found at {Path}", PrefsPath);
                return null;
            }

            var prefs = ParsePrefs(File.ReadAllText(PrefsPath));

            // Only report a port when ProPresenter's network service is enabled.
            if (!prefs.TryGetValue("EnableNetwork", out var enabled) ||
                !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
                return null;

            if (prefs.TryGetValue("NetworkPort", out var portStr) &&
                int.TryParse(portStr, out var port) && port is > 0 and < 65536)
            {
                _logger.LogDebug("Detected ProPresenter NetworkPort={Port} from {Path}", port, PrefsPath);
                return port;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Could not read ProPresenter port from preferences: {Message}", ex.Message);
        }

        return null;
    }

    // File format: lines of "Key=Value;" — semicolon-terminated, one per line.
    private static Dictionary<string, string> ParsePrefs(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim().TrimEnd(';');
            var eq = trimmed.IndexOf('=');
            if (eq > 0)
                result[trimmed[..eq].Trim()] = trimmed[(eq + 1)..].Trim();
        }
        return result;
    }
}
