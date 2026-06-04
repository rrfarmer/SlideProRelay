using SlideProRelay.Server.ProPresenter;

namespace SlideProRelay.Tests.ProPresenter;

public sealed class ProPresenterEndpointTests
{
    private sealed class FakeDetector(int? port) : IProPresenterPortDetector
    {
        public int? TryDetectNetworkPort() => port;
    }

    [Fact]
    public void Resolve_UsesDetectedPort_WhenAutoDetectAndLocalHost()
    {
        var opts = new ProPresenterOptions { Host = "localhost", Port = 50001, AutoDetectPort = true };

        var uri = ProPresenterEndpoint.Resolve(opts, new FakeDetector(65519), out var auto);

        Assert.Equal("http://localhost:65519/", uri.ToString());
        Assert.True(auto);
    }

    [Fact]
    public void Resolve_FallsBackToConfiguredPort_WhenDetectionFails()
    {
        var opts = new ProPresenterOptions { Host = "localhost", Port = 50001, AutoDetectPort = true };

        var uri = ProPresenterEndpoint.Resolve(opts, new FakeDetector(null), out var auto);

        Assert.Equal("http://localhost:50001/", uri.ToString());
        Assert.False(auto);
    }

    [Fact]
    public void Resolve_IgnoresDetection_WhenAutoDetectDisabled()
    {
        var opts = new ProPresenterOptions { Host = "localhost", Port = 50001, AutoDetectPort = false };

        var uri = ProPresenterEndpoint.Resolve(opts, new FakeDetector(65519), out var auto);

        Assert.Equal("http://localhost:50001/", uri.ToString());
        Assert.False(auto);
    }

    [Fact]
    public void Resolve_IgnoresDetection_WhenHostIsRemote()
    {
        // Auto-detect reads the LOCAL machine's prefs, so it must not apply to a remote host.
        var opts = new ProPresenterOptions { Host = "192.168.1.50", Port = 50001, AutoDetectPort = true };

        var uri = ProPresenterEndpoint.Resolve(opts, new FakeDetector(65519), out var auto);

        Assert.Equal("http://192.168.1.50:50001/", uri.ToString());
        Assert.False(auto);
    }
}
