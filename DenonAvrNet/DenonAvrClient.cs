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
    private IReadOnlyList<string>? _availableInputs;
    private bool _requiresSequentialAppCommandRequests;
    private bool _disposed;

    /// <summary>Creates a client for a receiver host or IP address.</summary>
    /// <param name="host">Receiver hostname, IP address or HTTP(S) base address.</param>
    /// <param name="requestTimeout">Optional timeout; defaults to five seconds.</param>
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

    /// <summary>Gets the normalized receiver hostname or IP address.</summary>
    public string Host { get; }

    /// <summary>Gets the HTTP port detected by <see cref="InitializeAsync"/>.</summary>
    public int? HttpPort { get; private set; }

    /// <summary>Gets the device information returned by the last initialization.</summary>
    public DenonDeviceInfo? DeviceInfo { get; private set; }

    /// <summary>Gets the most recently confirmed receiver state.</summary>
    public DenonReceiverState? State { get; private set; }

    /// <summary>Detects the receiver API and reads its device information.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The detected device information.</returns>
    /// <exception cref="DenonConnectionException">No supported API endpoint responded.</exception>
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
                State = null;
                _availableInputs = null;
                _requiresSequentialAppCommandRequests = false;
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

    /// <summary>Reads and stores a confirmed Main Zone status snapshot.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The newly read receiver state.</returns>
    /// <exception cref="InvalidOperationException">The client has not been initialized.</exception>
    /// <exception cref="DenonProtocolException">The receiver returned no usable basic state.</exception>
    public async Task<DenonReceiverState> UpdateAsync(CancellationToken cancellationToken = default)
    {
        var port = GetInitializedPort();

        if (port == 8080)
        {
            var availableInputs = _availableInputs ??
                await RefreshInputsAsync(cancellationToken).ConfigureAwait(false);
            var mainZoneState = await QueryMainZoneStatusAsync(
                port,
                availableInputs,
                cancellationToken).ConfigureAwait(false);
            var audioInfoXml = await TryQueryAppCommand0300Async(
                port,
                DenonAppCommand.CreateDetailedRequest(
                    DenonAppCommand.GetAudioInfo,
                    "inputmode",
                    "output",
                    "signal",
                    "sound",
                    "fs"),
                cancellationToken).ConfigureAwait(false);
            var activeSpeakersXml = await TryQueryAppCommand0300Async(
                port,
                DenonAppCommand.CreateDetailedRequest(
                    DenonAppCommand.GetActiveSpeaker,
                    "activespall"),
                cancellationToken).ConfigureAwait(false);

            State = mainZoneState with
            {
                Audio = DenonXmlParser.ParseAppCommandAudioInfo(
                    audioInfoXml,
                    activeSpeakersXml)
            };
        }
        else
        {
            var xml = await _httpTransport.GetStringAsync(
                Host,
                port,
                DenonEndpoints.MainZoneStatus,
                cancellationToken).ConfigureAwait(false);

            State = DenonXmlParser.ParseMainZoneStatus(xml);
            _availableInputs = State.AvailableInputs;
        }

        return State;
    }

    /// <summary>Refreshes and returns the receiver's currently enabled input list.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The enabled input display names reported by the receiver.</returns>
    public async Task<IReadOnlyList<string>> RefreshInputsAsync(
        CancellationToken cancellationToken = default)
    {
        var port = GetInitializedPort();
        IReadOnlyList<string> inputs;

        if (port == 8080)
        {
            var xml = await QueryAppCommandAsync(
                port,
                DenonAppCommand.GetDeletedSource,
                cancellationToken).ConfigureAwait(false);
            inputs = DenonXmlParser.ParseAppCommandAvailableInputs(xml);
        }
        else
        {
            var xml = await _httpTransport.GetStringAsync(
                Host,
                port,
                DenonEndpoints.MainZoneStatus,
                cancellationToken).ConfigureAwait(false);
            inputs = DenonXmlParser.ParseMainZoneStatus(xml).AvailableInputs;
        }

        _availableInputs = inputs;
        if (State is not null)
        {
            State = State with { AvailableInputs = inputs };
        }

        return inputs;
    }

    private async Task<DenonReceiverState> QueryMainZoneStatusAsync(
        int port,
        IReadOnlyList<string> availableInputs,
        CancellationToken cancellationToken)
    {
        if (!_requiresSequentialAppCommandRequests)
        {
            var bundledXml = await _httpTransport.PostXmlAsync(
                Host,
                port,
                DenonEndpoints.AppCommand,
                DenonAppCommand.CreateRequest(DenonAppCommand.MainZoneStatusCommands),
                cancellationToken).ConfigureAwait(false);

            try
            {
                return DenonXmlParser.ParseBundledAppCommandMainZoneStatus(
                    bundledXml,
                    availableInputs);
            }
            catch (DenonProtocolException)
            {
                // Remember the receiver's behavior so later updates can skip
                // the known-incompatible bundled request.
                _requiresSequentialAppCommandRequests = true;
            }
        }

        return await QuerySequentialMainZoneStatusAsync(
            port,
            availableInputs,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<DenonReceiverState> QuerySequentialMainZoneStatusAsync(
        int port,
        IReadOnlyList<string> availableInputs,
        CancellationToken cancellationToken)
    {
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

        return DenonXmlParser.ParseSeparateAppCommandMainZoneStatus(
            powerXml,
            volumeXml,
            muteXml,
            sourceXml,
            availableInputs);
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

    private async Task<string?> TryQueryAppCommand0300Async(
        int port,
        string request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpTransport.PostXmlAsync(
                Host,
                port,
                DenonEndpoints.AppCommand0300,
                request,
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
        {
            // Audio details are an optional capability. Receivers without the
            // AppCommand0300 endpoint must still return their basic state.
            return null;
        }
    }

    /// <summary>Switches the Main Zone on.</summary>
    public Task PowerOnAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(DenonEndpoints.PowerOn, cancellationToken);

    /// <summary>Switches the Main Zone to standby.</summary>
    public Task PowerOffAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(DenonEndpoints.PowerStandby, cancellationToken);

    /// <summary>Raises the Main Zone volume by one receiver step.</summary>
    public Task VolumeUpAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(DenonEndpoints.VolumeUp, cancellationToken);

    /// <summary>Lowers the Main Zone volume by one receiver step.</summary>
    public Task VolumeDownAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(DenonEndpoints.VolumeDown, cancellationToken);

    /// <summary>Sets the Main Zone volume, rounded to a half-decibel step.</summary>
    /// <param name="volumeDb">Volume from -80.0 through +18.0 dB.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
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

    /// <summary>Enables or disables Main Zone muting.</summary>
    /// <param name="muted"><see langword="true"/> to mute; otherwise <see langword="false"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public Task SetMuteAsync(bool muted, CancellationToken cancellationToken = default) =>
        SendCommandAsync(muted ? DenonEndpoints.MuteOn : DenonEndpoints.MuteOff, cancellationToken);

    /// <summary>Selects a Main Zone input using a display or Denon protocol name.</summary>
    /// <param name="input">Input such as <c>CBL/SAT</c>, <c>Media Player</c> or <c>MPLAY</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public Task SetInputAsync(string input, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (input.Contains('\r') || input.Contains('\n'))
        {
            throw new ArgumentException("Der Eingangsname darf keinen Zeilenumbruch enthalten.", nameof(input));
        }

        var protocolName = DenonInputSource.ToProtocolName(input.Trim());
        return SendCommandAsync(DenonEndpoints.SetInput(protocolName), cancellationToken);
    }

    /// <summary>Sends a complete Denon HTTP command path.</summary>
    /// <param name="commandPath">Path beginning with <c>/</c>, including any query command.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
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

    /// <inheritdoc />
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
