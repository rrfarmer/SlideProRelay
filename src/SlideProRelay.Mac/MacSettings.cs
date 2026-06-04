using System.Text.Json;

namespace SlideProRelay.Mac;

public sealed class MacSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlideProRelay", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Host { get; set; } = "localhost";
    public int ProPresenterPort { get; set; } = 50001;
    public int PollingIntervalMs { get; set; } = 500;
    public int RelayPort { get; set; } = 5174;

    public static MacSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<MacSettings>(json) ?? new MacSettings();
            }
        }
        catch { }

        return new MacSettings();
    }

    public static void Save(MacSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public IEnumerable<KeyValuePair<string, string?>> ToConfigOverrides() =>
    [
        new("ProPresenter:Host", Host),
        new("ProPresenter:Port", ProPresenterPort.ToString()),
        new("ProPresenter:PollingIntervalMs", PollingIntervalMs.ToString()),
        new("Relay:Port", RelayPort.ToString()),
    ];
}
