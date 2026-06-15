using System.Text.Json;

namespace SlideProRelay.Tray;

internal sealed class TraySettings
{
    internal static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlideProRelay", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Host { get; set; } = "localhost";
    public int ProPresenterPort { get; set; } = 50001;
    public int PollingIntervalMs { get; set; } = 500;
    public int RelayPort { get; set; } = 5174;

    /// <summary>Capture the real output display on each slide change (served at /api/screen-capture).</summary>
    public bool ScreenCaptureEnabled { get; set; } = true;

    /// <summary>1-based display to capture; 0 = auto-match ProPresenter's audience screen.</summary>
    public int CaptureDisplayIndex { get; set; } = 0;

    /// <summary>SlidePro.io integration mode: "off", "text", or "screencapture".</summary>
    public string SlideProMode { get; set; } = "off";

    /// <summary>SlidePro API key — entered by the user in tray settings, never in appsettings.</summary>
    public string SlideProApiKey { get; set; } = "";

    /// <summary>Target presentation GUID selected by the user from their SlidePro account.</summary>
    public string SlideProPresentationId { get; set; } = "";

    public static TraySettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<TraySettings>(json) ?? new TraySettings();
            }
        }
        catch { }

        return new TraySettings();
    }

    public static void Save(TraySettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public IEnumerable<KeyValuePair<string, string?>> ToConfigOverrides() =>
    [
        new("Settings:FilePath", SettingsPath),
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
