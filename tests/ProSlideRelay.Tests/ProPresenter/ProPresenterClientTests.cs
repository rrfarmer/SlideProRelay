using Microsoft.Extensions.Logging.Abstractions;
using ProSlideRelay.Server.ProPresenter;
using ProSlideRelay.Server.ProPresenter.Models;

namespace ProSlideRelay.Tests.ProPresenter;

public sealed class ProPresenterClientTests
{
    private static ProPresenterClient BuildClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:50001") };
        return new ProPresenterClient(http, NullLogger<ProPresenterClient>.Instance);
    }

    [Fact]
    public async Task GetCurrentSlide_WhenConnected_ReturnsCurrentAndNextText()
    {
        const string json = """
            {
              "current": { "uuid": "abc-1", "text": "Amazing grace", "notes": null },
              "next":    { "uuid": "abc-2", "text": "How sweet the sound", "notes": null }
            }
            """;

        var client = BuildClient(new FakeHttpHandler(json));
        var status = await client.GetCurrentSlideAsync();

        Assert.Equal(ConnectionState.Connected, status.Connection);
        Assert.Equal("abc-1", status.Current?.Uuid);
        Assert.Equal("Amazing grace", status.Current?.Text);
        Assert.Equal("abc-2", status.Next?.Uuid);
        Assert.Equal("How sweet the sound", status.Next?.Text);
    }

    [Fact]
    public async Task GetCurrentSlide_WhenProPresenterUnreachable_ReturnsUnavailable()
    {
        var client = BuildClient(new ThrowingHttpHandler(new HttpRequestException("Connection refused")));
        var status = await client.GetCurrentSlideAsync();

        Assert.Equal(ConnectionState.Unavailable, status.Connection);
        Assert.Null(status.Current);
        Assert.Null(status.Next);
    }

    [Fact]
    public async Task GetCurrentSlide_WhenResponseHasNullSlides_ReturnsConnectedWithNulls()
    {
        const string json = """{ "current": null, "next": null }""";

        var client = BuildClient(new FakeHttpHandler(json));
        var status = await client.GetCurrentSlideAsync();

        Assert.Equal(ConnectionState.Connected, status.Connection);
        Assert.Null(status.Current);
        Assert.Null(status.Next);
    }

    [Fact]
    public async Task GetCurrentSlide_UpdatedAt_IsRecentUtcTimestamp()
    {
        const string json = """{ "current": null, "next": null }""";
        var before = DateTimeOffset.UtcNow;

        var client = BuildClient(new FakeHttpHandler(json));
        var status = await client.GetCurrentSlideAsync();

        Assert.True(status.UpdatedAt >= before);
        Assert.True(status.UpdatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetVersion_WhenConnected_ReturnsName()
    {
        const string json = """{ "name": "ProPresenter", "version": "7.14.1" }""";

        var client = BuildClient(new FakeHttpHandler(json));
        var version = await client.GetVersionAsync();

        Assert.Equal("ProPresenter", version);
    }

    [Fact]
    public async Task GetVersion_WhenUnreachable_ReturnsNull()
    {
        var client = BuildClient(new ThrowingHttpHandler(new HttpRequestException("Connection refused")));
        var version = await client.GetVersionAsync();

        Assert.Null(version);
    }
}
