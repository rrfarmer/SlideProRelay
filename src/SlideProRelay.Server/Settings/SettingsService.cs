using System.Text.Json;

namespace SlideProRelay.Server.Settings;

/// <summary>
/// Reads and writes the settings JSON file whose path is supplied via the
/// "Settings:FilePath" configuration key (injected by the tray / mac app on startup).
/// When running in standalone mode (no file path configured), IsConfigured is false
/// and the settings endpoints return 503.
/// </summary>
internal sealed class SettingsService
{
    private readonly string? _filePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public SettingsService(IConfiguration config)
    {
        _filePath = config["Settings:FilePath"];
    }

    public bool IsConfigured => _filePath is not null;

    public RelaySettings? Load()
    {
        if (_filePath is null || !File.Exists(_filePath)) return null;
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<RelaySettings>(json, JsonOptions);
        }
        catch { return null; }
    }

    public void Save(RelaySettings settings)
    {
        if (_filePath is null)
            throw new InvalidOperationException("Settings:FilePath not configured.");
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
