using System.Text.Json.Serialization;

namespace SlideProRelay.Server.ProPresenter.Api;

internal sealed class ProPresenterSlideResponse
{
    [JsonPropertyName("current")]
    public ProPresenterSlideEntry? Current { get; init; }

    [JsonPropertyName("next")]
    public ProPresenterSlideEntry? Next { get; init; }
}

internal sealed class ProPresenterSlideEntry
{
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}
