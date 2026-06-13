using System.Text.Json.Serialization;

namespace SlideProRelay.Server.SlidePro;

public sealed record PresentationDto(
    [property: JsonPropertyName("presentationId")] string PresentationId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("date")] string? Date,
    [property: JsonPropertyName("speakerName")] string? SpeakerName,
    [property: JsonPropertyName("location")] string? Location,
    [property: JsonPropertyName("status")] int Status);

public sealed record UploadSlideResponseDto(
    [property: JsonPropertyName("slideId")] int SlideId,
    [property: JsonPropertyName("slideNumber")] int SlideNumber,
    [property: JsonPropertyName("presentationId")] string PresentationId,
    [property: JsonPropertyName("isPresented")] bool IsPresented,
    [property: JsonPropertyName("presentedTimestamp")] string? PresentedTimestamp,
    [property: JsonPropertyName("filePublicUrl")] string? FilePublicUrl,
    [property: JsonPropertyName("fileId")] int FileId,
    [property: JsonPropertyName("contentKind")] string? ContentKind);

public sealed record RepresentSlideResponseDto(
    [property: JsonPropertyName("slideId")] int SlideId,
    [property: JsonPropertyName("slideNumber")] int SlideNumber,
    [property: JsonPropertyName("presentationId")] string PresentationId,
    [property: JsonPropertyName("isPresented")] bool IsPresented,
    [property: JsonPropertyName("presentedTimestamp")] string? PresentedTimestamp);
