using System.Net;
using System.Text;

namespace ProSlideRelay.Tests.ProPresenter;

internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _body;

    public FakeHttpHandler(string body, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _body = body;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

internal sealed class ThrowingHttpHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHttpHandler(Exception exception) => _exception = exception;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(_exception);
}
