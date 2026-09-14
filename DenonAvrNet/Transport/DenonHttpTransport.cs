using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace DenonAvrNet.Transport;

internal sealed class DenonHttpTransport : IDisposable
{
    private readonly HttpClient _httpClient;

    internal DenonHttpTransport(TimeSpan timeout)
        : this(CreateDefaultHandler(timeout), timeout)
    {
    }

    internal DenonHttpTransport(HttpMessageHandler handler, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        _httpClient = new HttpClient(handler)
        {
            Timeout = timeout
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("DenonAvrNet", "0.1"));
    }

    internal async Task<string> GetStringAsync(
        string host,
        int port,
        string pathAndQuery,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildUri(host, port, pathAndQuery);
        using var response = await _httpClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task<string> PostXmlAsync(
        string host,
        int port,
        string path,
        string xml,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(xml);

        var requestUri = BuildUri(host, port, path);
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml")
        {
            CharSet = "utf-8"
        };
        using var response = await _httpClient.PostAsync(
            requestUri,
            content,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _httpClient.Dispose();

    private static HttpMessageHandler CreateDefaultHandler(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        return new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            ConnectTimeout = timeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
    }

    private static Uri BuildUri(string host, int port, string pathAndQuery)
    {
        if (!pathAndQuery.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Ein Denon-Befehlspfad muss mit '/' beginnen.", nameof(pathAndQuery));
        }

        var builder = new UriBuilder(Uri.UriSchemeHttp, host, port);
        var queryStart = pathAndQuery.IndexOf('?', StringComparison.Ordinal);

        if (queryStart < 0)
        {
            builder.Path = pathAndQuery;
        }
        else
        {
            builder.Path = pathAndQuery[..queryStart];
            builder.Query = pathAndQuery[(queryStart + 1)..];
        }

        return builder.Uri;
    }
}
