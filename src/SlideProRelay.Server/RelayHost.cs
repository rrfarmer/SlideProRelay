using SlideProRelay.Server.ProPresenter;

namespace SlideProRelay.Server;

/// <summary>
/// Wraps WebApplication so callers (e.g. the tray app) don't need
/// a direct reference to the ASP.NET Core shared framework.
/// </summary>
public sealed class RelayHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private RelayHost(WebApplication app) => _app = app;

    public static RelayHost Create(IEnumerable<KeyValuePair<string, string?>> configOverrides) =>
        new(ServerHost.Create([], configOverrides));

    public Task StartAsync(CancellationToken ct = default) => _app.StartAsync(ct);
    public Task StopAsync(CancellationToken ct = default) => _app.StopAsync(ct);

    public SlideCache Cache => _app.Services.GetRequiredService<SlideCache>();

    public IReadOnlyList<string> Urls => [.. _app.Urls];

    /// <summary>
    /// The ProPresenter port the relay is actually talking to right now.
    /// When auto-detection is on this may differ from the configured port.
    /// </summary>
    public int ActiveProPresenterPort =>
        _app.Services.GetRequiredService<ProPresenterEndpoint>().Current.Port;

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
