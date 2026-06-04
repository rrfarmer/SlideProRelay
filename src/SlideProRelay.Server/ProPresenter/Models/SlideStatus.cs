namespace SlideProRelay.Server.ProPresenter.Models;

public sealed record SlideStatus(
    SlideInfo? Current,
    SlideInfo? Next,
    ConnectionState Connection,
    DateTimeOffset UpdatedAt);
