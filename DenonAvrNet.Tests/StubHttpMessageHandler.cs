using System.Net;

namespace DenonAvrNet.Tests;

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory)
    : HttpMessageHandler
{
    internal List<Uri> RequestedUris { get; } = [];

    internal List<HttpMethod> RequestMethods { get; } = [];

    internal List<string?> RequestBodies { get; } = [];

    internal List<string?> RequestContentTypes { get; } = [];

    internal List<string?> RequestContentCharsets { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestedUris.Add(request.RequestUri ?? throw new InvalidOperationException("Request URI fehlt."));
        RequestMethods.Add(request.Method);
        RequestBodies.Add(request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken));
        RequestContentTypes.Add(request.Content?.Headers.ContentType?.MediaType);
        RequestContentCharsets.Add(request.Content?.Headers.ContentType?.CharSet);
        return responseFactory(request, cancellationToken);
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
