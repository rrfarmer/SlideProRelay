namespace SlideProRelay.Server.SlidePro;

public sealed class SlideProOptions
{
    public const string SectionName = "SlidePro";

    /// <summary>Base URL for the SlidePro public API. Configurable in appsettings.json.</summary>
    public string BaseUrl { get; set; } = "https://slidepro.io/api/public/v1";

    /// <summary>API key — supplied by the tray app via config overrides, never in appsettings.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Whether to relay screen captures to SlidePro on each slide change.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Target presentation GUID — selected by the user in the tray settings UI.</summary>
    public string PresentationId { get; set; } = "";
}
