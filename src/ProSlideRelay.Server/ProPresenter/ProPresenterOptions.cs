namespace ProSlideRelay.Server.ProPresenter;

public sealed class ProPresenterOptions
{
    public const string SectionName = "ProPresenter";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 50001;
    public int PollingIntervalMs { get; init; } = 500;
}
