using System.Globalization;
using DenonAvrNet.Exceptions;
using DenonAvrNet.Models;
using DenonAvrNet.Protocol;
using DenonAvrNet.Transport;

namespace DenonAvrNet;

/// <summary>Controls a Denon or compatible Marantz receiver through its HTTP/XML API.</summary>
public sealed class DenonAvrClient : IDisposable
{
    private readonly DenonHttpTransport _httpTransport;
    private readonly bool _ownsTransport;
    private bool _disposed;

    public DenonAvrClient(string host, TimeSpan? requestTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        Host = NormalizeHost(host);
        _httpTransport = new DenonHttpTransport(requestTimeout ?? TimeSpan.FromSeconds(5));
        _ownsTransport = true;
    }

    internal DenonAvrClient(string host, DenonHttpTransport httpTransport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        Host = NormalizeHost(host);
        _httpTransport = httpTransport ?? throw new ArgumentNullException(nameof(httpTransport));
    }

    public string Host { get; }

    public int? HttpPort { get; private set; }

    public DenonDeviceInfo? DeviceInfo { get; private set; }

    public DenonReceiverState? State { get; private set; }

    public async Task<DenonDeviceInfo> InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        List<Exception> failures = [];

        foreach (var port in new[] { 80, 8080 })
        {
            try
            {
                var xml = await _httpTransport.GetStringAsync(
                    Host,
                    port,
                    DenonEndpoints.DeviceInfo,
                    cancellationToken).ConfigureAwait(false);

                var deviceInfo = DenonXmlParser.ParseDeviceInfo(xml);
                HttpPort = port;
                DeviceInfo = deviceInfo;
                return deviceInfo;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                              exception is HttpRequestException
                                                  or TaskCanceledException
                                                  or DenonProtocolException)
            {
                failures.Add(exception);
            }
        }

        throw new DenonConnectionException(
            $"Der Receiver '{Host}' antwortet weder auf Port 80 noch auf Port 8080 mit gültigen Geräteinformationen.",
            new AggregateException(failures));
    }

    public async Task<DenonReceiverState> UpdateAsync(CancellationToken cancellationToken = default)
    {
        var port = GetInitializedPort();

        if (port == 8080)
        {
            // Keep AppCommand calls sequential. Some receiver firmware does not
            // reliably return every result from bundled or concurrent queries.
            var powerXml = await QueryAppCommandAsync(
                port,
                DenonAppCommand.GetAllZonePowerStatus,
                cancellationToken).ConfigureAwait(false);
            var volumeXml = await QueryAppCommandAsync(
                port,
                DenonAppCommand.GetAllZoneVolume,
                cancellationToken).ConfigureAwait(false);
            var muteXml = await QueryAppCommandAsync(
                port,
                DenonAppCommand.GetAllZoneMuteStatus,
                cancellationToken).ConfigureAwait(false);
            var sourceXml = await QueryAppCommandAsync(
                port,
                DenonAppCommand.GetAllZoneSource,
                cancellationToken).ConfigureAwait(false);
            var deletedSourcesXml = await QueryAppCommandAsync(
                port,
                DenonAppCommand.GetDeletedSource,
                cancellationToken).ConfigureAwait(false);

            State = DenonXmlParser.ParseAppCommandMainZoneStatus(
                powerXml,
                volumeXml,
                muteXml,
                sourceXml,
                deletedSourcesXml);
        }
        else
        {
            var xml = await _httpTransport.GetStringAsync(
                Host,
                port,
                DenonEndpoints.MainZoneStatus,
                cancellationToken).ConfigureAwait(false);

            State = DenonXmlParser.ParseMainZoneStatus(xml);
        }

        return State;
    }

    private Task<string> QueryAppCommandAsync(
        int port,
        string command,
        CancellationToken cancellationToken) =>
        _httpTransport.PostXmlAsync(
            Host,
            port,
            DenonEndpoints.AppCommand,
            DenonAppCommand.CreateRequest(command),
            cancellationToken);

    public Task PowerOnAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(DenonEndpoints.PowerOn, cancellationToken);

    public Task PowerOffAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(DenonEndpoints.PowerStandby, cancellationToken);

    public Task VolumeUpAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(DenonEndpoints.VolumeUp, cancellationToken);

    public Task VolumeDownAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(DenonEndpoints.VolumeDown, cancellationToken);

    public Task SetVolumeAsync(double volumeDb, CancellationToken cancellationToken = default)
    {
        if (volumeDb is < -80.0 or > 18.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(volumeDb),
                volumeDb,
                "Die Lautstärke muss zwischen -80,0 und +18,0 dB liegen.");
        }

        var roundedVolume = Math.Round(volumeDb * 2, MidpointRounding.ToEven) / 2.0;
        var value = roundedVolume.ToString("0.0", CultureInfo.InvariantCulture);
        return SendCommandAsync(DenonEndpoints.SetVolume(value), cancellationToken);
    }

    public Task SetMuteAsync(bool muted, CancellationToken cancellationToken = default) =>
        SendCommandAsync(muted ? DenonEndpoints.MuteOn : DenonEndpoints.MuteOff, cancellationToken);

    public Task SetInputAsync(string input, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (input.Contains('\r') || input.Contains('\n'))
        {
            throw new ArgumentException("Der Eingangsname darf keinen Zeilenumbruch enthalten.", nameof(input));
        }

        return SendCommandAsync(DenonEndpoints.SetInput(input.Trim()), cancellationToken);
    }

    public async Task SendCommandAsync(
        string commandPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandPath);
        var port = GetInitializedPort();

        _ = await _httpTransport.GetStringAsync(
            Host,
            port,
            commandPath,
            cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsTransport)
        {
            _httpTransport.Dispose();
        }

        _disposed = true;
    }

    private int GetInitializedPort()
    {
        ThrowIfDisposed();
        return HttpPort ?? throw new InvalidOperationException(
            "InitializeAsync muss vor dem Senden von Befehlen aufgerufen werden.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static string NormalizeHost(string host)
    {
        var normalized = host.Trim();

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme is not ("http" or "https"))
            {
                throw new ArgumentException("Es werden nur HTTP-Adressen unterstützt.", nameof(host));
            }

            return uri.Host;
        }

        return normalized.Trim('[', ']');
    }
}
