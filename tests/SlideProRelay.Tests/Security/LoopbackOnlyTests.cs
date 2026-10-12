using System.Net;
using SlideProRelay.Server.Security;

namespace SlideProRelay.Tests.Security;

public sealed class LoopbackOnlyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")]           // all of 127/8 loops back
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]    // IPv4 client seen through a dual-stack listener
    public void IsLoopback_AcceptsLoopbackAddresses(string address) =>
        Assert.True(LoopbackOnly.IsLoopback(IPAddress.Parse(address)));

    [Theory]
    [InlineData("192.168.1.20")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.9")]
    [InlineData("::ffff:192.168.1.20")] // IPv4-mapped LAN address
    [InlineData("fe80::1")]             // link-local IPv6
    [InlineData("2001:db8::1")]
    [InlineData("0.0.0.0")]
    public void IsLoopback_RejectsLanAddresses(string address) =>
        Assert.False(LoopbackOnly.IsLoopback(IPAddress.Parse(address)));

    [Fact]
    public void IsLoopback_RejectsUnknownAddress() =>
        Assert.False(LoopbackOnly.IsLoopback(null));

    [Theory]
    [InlineData("http://localhost:5174")]
    [InlineData("http://LOCALHOST:5175")]
    [InlineData("https://localhost")]
    [InlineData("http://127.0.0.1:5174")]
    [InlineData("http://[::1]:5174")]
    public void IsLoopbackOrigin_AcceptsLocalOrigins(string origin) =>
        Assert.True(LoopbackOnly.IsLoopbackOrigin(origin));

    [Theory]
    [InlineData("http://192.168.1.20:5174")]
    [InlineData("http://localhost.example.com:5174")]
    [InlineData("http://example.com")]
    [InlineData("file://localhost/index.html")]
    [InlineData("null")]                // opaque origin
    [InlineData("")]
    [InlineData(null)]
    public void IsLoopbackOrigin_RejectsEverythingElse(string? origin) =>
        Assert.False(LoopbackOnly.IsLoopbackOrigin(origin));
}
