using System.Diagnostics;
using System.Runtime.Versioning;

namespace SlideProRelay.Server.ProPresenter;

/// <summary>
/// Reads ProPresenter's network port from its macOS preferences domain
/// (com.renewedvision.propresenter → networkPort / networkEnabled).
/// ProPresenter assigns this port automatically and persists it, so reading it
/// lets the relay follow the port without manual configuration.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacProPresenterPortDetector : IProPresenterPortDetector
{
    private const string Domain = "com.renewedvision.propresenter";

    private readonly ILogger<MacProPresenterPortDetector> _logger;

    public MacProPresenterPortDetector(ILogger<MacProPresenterPortDetector> logger) => _logger = logger;

    public int? TryDetectNetworkPort()
    {
        if (!OperatingSystem.IsMacOS())
            return null;

        try
        {
            // Only report a port when ProPresenter's network service is enabled.
            if (ReadDefault("networkEnabled") is not "1")
                return null;

            if (int.TryParse(ReadDefault("networkPort"), out var port) && port is > 0 and < 65536)
            {
                _logger.LogDebug("Detected ProPresenter networkPort={Port} from macOS preferences", port);
                return port;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Could not read ProPresenter port from preferences: {Message}", ex.Message);
        }

        return null;
    }

    private static string? ReadDefault(string key)
    {
        // `defaults read <domain> <key>` returns the live value cfprefsd holds
        // for the running app, and exits non-zero if the key/domain is missing.
        var psi = new ProcessStartInfo("defaults")
        {
            ArgumentList = { "read", Domain, key },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var p = Process.Start(psi);
        if (p is null)
            return null;

        var output = p.StandardOutput.ReadToEnd();
        if (!p.WaitForExit(2000))
        {
            try { p.Kill(); } catch { /* best effort */ }
            return null;
        }

        return p.ExitCode == 0 ? output.Trim() : null;
    }
}
