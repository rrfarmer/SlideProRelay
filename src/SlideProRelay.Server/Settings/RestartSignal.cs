namespace SlideProRelay.Server.Settings;

/// <summary>
/// Singleton event bus that lets the web settings endpoint ask the host
/// (tray / mac app) to reload settings and restart the server.
/// The host subscribes via RelayHost.RestartRequested.
/// </summary>
internal sealed class RestartSignal
{
    public event Action? RestartRequested;
    public void SignalRestart() => RestartRequested?.Invoke();
}
