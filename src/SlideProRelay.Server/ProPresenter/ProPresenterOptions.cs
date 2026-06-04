namespace SlideProRelay.Server.ProPresenter;

public sealed class ProPresenterOptions
{
    public const string SectionName = "ProPresenter";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 50001;
    public int PollingIntervalMs { get; init; } = 500;

    /// <summary>
    /// When true (default) and the host is local, the port is auto-detected from
    /// ProPresenter's own preferences, overriding <see cref="Port"/>. Set false to
    /// force the configured <see cref="Port"/> (e.g. when pointing at a fixed port
    /// or a remote machine).
    /// </summary>
    public bool AutoDetectPort { get; init; } = true;
}
