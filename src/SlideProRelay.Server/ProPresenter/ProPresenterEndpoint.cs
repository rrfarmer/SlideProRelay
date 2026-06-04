using Microsoft.Extensions.Options;

namespace SlideProRelay.Server.ProPresenter;

/// <summary>
/// Resolves the base URI of ProPresenter's API and keeps it current. When
/// auto-detection is enabled and the host is local, the port is read from
/// ProPresenter's own preferences (which it assigns automatically), so the relay
/// follows ProPresenter's shifting port with no manual configuration. Call
/// <see cref="Refresh"/> to re-resolve — e.g. after a failed poll, so the relay
/// self-heals if ProPresenter restarts on a new port.
/// </summary>
public sealed class ProPresenterEndpoint
{
    private readonly IOptions<ProPresenterOptions> _options;
    private readonly IProPresenterPortDetector _detector;
    private readonly ILogger<ProPresenterEndpoint> _logger;

    private volatile Uri _current;
    private int _lastLoggedPort;

    public ProPresenterEndpoint(
        IOptions<ProPresenterOptions> options,
        IProPresenterPortDetector detector,
        ILogger<ProPresenterEndpoint> logger)
    {
        _options = options;
        _detector = detector;
        _logger = logger;
        _current = Resolve(options.Value, detector, out _);
        _lastLoggedPort = _current.Port;
        _logger.LogInformation("ProPresenter endpoint resolved to {Uri}", _current);
    }

    /// <summary>The current base URI, e.g. http://localhost:65519.</summary>
    public Uri Current => _current;

    /// <summary>Builds an absolute URI for a relative API path.</summary>
    public Uri Url(string relativePath) => new(_current, relativePath);

    /// <summary>Re-resolves the endpoint (re-reading the detected port).</summary>
    public void Refresh()
    {
        var resolved = Resolve(_options.Value, _detector, out _);
        _current = resolved;
        if (resolved.Port != _lastLoggedPort)
        {
            _logger.LogInformation("ProPresenter endpoint changed to {Uri}", resolved);
            _lastLoggedPort = resolved.Port;
        }
    }

    /// <summary>
    /// Pure resolution logic (no I/O beyond the detector) — unit-testable.
    /// </summary>
    public static Uri Resolve(ProPresenterOptions options, IProPresenterPortDetector detector, out bool autoDetected)
    {
        autoDetected = false;
        var port = options.Port;

        if (options.AutoDetectPort && IsLocal(options.Host))
        {
            var detected = detector.TryDetectNetworkPort();
            if (detected is int p)
            {
                port = p;
                autoDetected = true;
            }
        }

        return new Uri($"http://{options.Host}:{port}");
    }

    private static bool IsLocal(string host) =>
        host is "localhost" or "127.0.0.1" or "::1" or "[::1]";
}
