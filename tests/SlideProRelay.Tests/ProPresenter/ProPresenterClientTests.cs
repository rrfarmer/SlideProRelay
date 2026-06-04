using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SlideProRelay.Server.ProPresenter;
using SlideProRelay.Server.ProPresenter.Models;

namespace SlideProRelay.Tests.ProPresenter;

public sealed class ProPresenterClientTests
{
    private static ProPresenterClient BuildClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new ProPresenterOptions { Host = "localhost", Port = 50001 });
        var endpoint = new ProPresenterEndpoint(
            options, new NullProPresenterPortDetector(), NullLogger<ProPresenterEndpoint>.Instance);
        return new ProPresenterClient(http, endpoint, NullLogger<ProPresenterClient>.Instance);
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

    [Theory]
    [InlineData(System.Net.HttpStatusCode.NotFound)]
    [InlineData(System.Net.HttpStatusCode.InternalServerError)]
    [InlineData(System.Net.HttpStatusCode.NoContent)]
    public async Task GetCurrentSlide_WhenReachableButNonSuccess_ReturnsConnectedNoContent(
        System.Net.HttpStatusCode statusCode)
    {
        // "Clear All" in ProPresenter can yield a non-success status with nothing
        // live. That's reachable-but-empty, which must NOT read as disconnected.
        var client = BuildClient(new FakeHttpHandler("", statusCode));
        var status = await client.GetCurrentSlideAsync();

        Assert.Equal(ConnectionState.Connected, status.Connection);
        Assert.Null(status.Current);
        Assert.Null(status.Next);
    }

    [Fact]
    public async Task GetCurrentSlide_WhenReachableButEmptyBody_ReturnsConnectedNoContent()
    {
        // 200 OK with an empty body — another "cleared" shape. Reachable, no content.
        var client = BuildClient(new FakeHttpHandler(""));
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
