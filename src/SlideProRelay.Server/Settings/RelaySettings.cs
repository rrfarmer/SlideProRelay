namespace SlideProRelay.Server.Settings;

/// <summary>
/// Persisted settings model — shared schema between tray/mac app files and the web settings API.
/// Property names must stay in sync with TraySettings / MacSettings.
/// </summary>
public sealed class RelaySettings
{
    public string Host { get; set; } = "localhost";
    public int ProPresenterPort { get; set; } = 50001;
    public int PollingIntervalMs { get; set; } = 500;
    public int RelayPort { get; set; } = 5174;
    public bool ScreenCaptureEnabled { get; set; } = true;
    public int CaptureDisplayIndex { get; set; } = 0;
    public string SlideProMode { get; set; } = "off";
    public string SlideProApiKey { get; set; } = "";
    public string SlideProPresentationId { get; set; } = "";

    public IEnumerable<KeyValuePair<string, string?>> ToConfigOverrides() =>
    [
        new("ProPresenter:Host", Host),
        new("ProPresenter:Port", ProPresenterPort.ToString()),
        new("ProPresenter:PollingIntervalMs", PollingIntervalMs.ToString()),
        new("Relay:Port", RelayPort.ToString()),
        new("ScreenCapture:Enabled", ScreenCaptureEnabled.ToString()),
        new("ScreenCapture:DisplayIndex", CaptureDisplayIndex.ToString()),
        new("SlidePro:Enabled", (SlideProMode == "screencapture").ToString()),
        new("SlidePro:SendTextUpdates", (SlideProMode == "text").ToString()),
        new("SlidePro:ApiKey", SlideProApiKey),
        new("SlidePro:PresentationId", SlideProPresentationId),
    ];
}
