using System.Net;

namespace DenonAvrNet.Tests;

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory)
    : HttpMessageHandler
{
    internal List<Uri> RequestedUris { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestedUris.Add(request.RequestUri ?? throw new InvalidOperationException("Request URI fehlt."));
        return Task.FromResult(responseFactory(request, cancellationToken));
    }

    internal static HttpResponseMessage Xml(string xml) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(xml, System.Text.Encoding.UTF8, "application/xml")
    };

    internal static HttpResponseMessage Ok() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(string.Empty)
    };
}
