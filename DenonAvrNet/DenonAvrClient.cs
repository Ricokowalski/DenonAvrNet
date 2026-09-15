using System.Globalization;
using System.Net.Sockets;
using DenonAvrNet.Exceptions;
using DenonAvrNet.Models;
using DenonAvrNet.Protocol;
using DenonAvrNet.Profiles;
using DenonAvrNet.Transport;

namespace DenonAvrNet;

/// <summary>Controls a Denon or compatible Marantz receiver through its HTTP/XML API.</summary>
public sealed class DenonAvrClient : IDisposable
{
    private readonly DenonHttpTransport _httpTransport;
    private readonly DenonTelnetClient _telnetClient;
    private readonly bool _ownsTransport;
    private static readonly IReadOnlySet<DenonControlProtocol> HttpAndTelnet =
        new HashSet<DenonControlProtocol>
        {
            DenonControlProtocol.Http,
            DenonControlProtocol.Telnet
        };
    private static readonly IReadOnlySet<DenonControlProtocol> HttpOnly =
        new HashSet<DenonControlProtocol> { DenonControlProtocol.Http };
    private static readonly IReadOnlySet<DenonControlProtocol> TelnetOnly =
        new HashSet<DenonControlProtocol> { DenonControlProtocol.Telnet };
    private IReadOnlyList<string>? _availableInputs;
    private IDenonReceiverProfile? _receiverProfile;
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
        _telnetClient = new DenonTelnetClient(Host, requestTimeout);
        _ownsTransport = true;
    }

    internal DenonAvrClient(string host, DenonHttpTransport httpTransport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        Host = NormalizeHost(host);
        _httpTransport = httpTransport ?? throw new ArgumentNullException(nameof(httpTransport));
        _telnetClient = new DenonTelnetClient(Host);
    }

    /// <summary>Gets the normalized receiver hostname or IP address.</summary>
    public string Host { get; }

    /// <summary>
    /// Gets or sets the default transport for control methods. The default <see cref="DenonControlProtocol.Auto"/>
    /// retains HTTP control after initialization and makes Telnet-only receivers usable without HTTP initialization.
    /// </summary>
    public DenonControlProtocol PreferredControlProtocol { get; set; } = DenonControlProtocol.Auto;

    /// <summary>Gets the HTTP port detected by <see cref="InitializeAsync"/>.</summary>
    public int? HttpPort { get; private set; }

    /// <summary>Gets the device information returned by the last initialization.</summary>
    public DenonDeviceInfo? DeviceInfo { get; private set; }

    /// <summary>
    /// Gets the detected capabilities of this concrete receiver, or <see langword="null"/>
    /// until <see cref="InitializeAsync"/> has succeeded.
    /// </summary>
    public DenonReceiverCapabilities? ReceiverCapabilities { get; private set; }

    /// <summary>
    /// Gets the HTTP/API profile selected for the connected receiver, or <see langword="null"/>
    /// until <see cref="InitializeAsync"/> has succeeded.
    /// </summary>
    public string? ReceiverProfileId => _receiverProfile?.Id;

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
                _receiverProfile = DenonReceiverProfileRegistry.Select(deviceInfo, port);
                ReceiverCapabilities = CreateReceiverCapabilities(deviceInfo, port);
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
            var capabilities = ReceiverCapabilities;
            if (capabilities is not null)
            {
                ReceiverCapabilities = capabilities with
                {
                    SupportsAppCommand0300 = audioInfoXml is not null || activeSpeakersXml is not null
                };
            }
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
            var capabilities = ReceiverCapabilities;
            if (capabilities is not null)
            {
                ReceiverCapabilities = capabilities with { SupportsAppCommand0300 = false };
            }
        }

        return State;
    }

    /// <summary>
    /// Returns the transports implemented by this library for the specified logical operation.
    /// This describes the library/protocol layer only; use <see cref="ReceiverCapabilities"/>
    /// or <see cref="IsFeatureAvailable"/> to inspect the connected hardware as well.
    /// </summary>
    public IReadOnlySet<DenonControlProtocol> GetSupportedProtocols(AvrFeature feature) => feature switch
    {
        AvrFeature.MainZonePower or
        AvrFeature.MainZoneVolume or
        AvrFeature.MainZoneMute or
        AvrFeature.MainZoneInput => HttpAndTelnet,
        AvrFeature.MainZoneStatus or
        AvrFeature.AudioInformation or
        AvrFeature.ActiveSpeakerStatus or
        AvrFeature.SpeakerPresetLevelControl => HttpOnly,
        AvrFeature.Zone2Control or
        AvrFeature.Zone3Control or
        AvrFeature.LiveEvents or
        AvrFeature.ChannelLevelRead or
        AvrFeature.ChannelLevelControl or
        AvrFeature.SpeakerPresetControl or
        AvrFeature.SurroundModeControl or
        AvrFeature.DigitalInputModeControl => TelnetOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, null)
    };

    /// <summary>
    /// Determines whether the detected receiver supports a logical feature. Returns
    /// <see langword="false"/> before initialization because the hardware is then unknown.
    /// </summary>
    public bool IsFeatureAvailable(AvrFeature feature)
    {
        var capabilities = ReceiverCapabilities;
        if (capabilities is null)
        {
            return false;
        }

        return feature switch
        {
            AvrFeature.Zone2Control => capabilities.SupportsZone2,
            AvrFeature.Zone3Control => capabilities.SupportsZone3,
            AvrFeature.AudioInformation or AvrFeature.ActiveSpeakerStatus =>
                capabilities.SupportsAppCommand0300 == true,
            AvrFeature.MainZonePower or
            AvrFeature.MainZoneVolume or
            AvrFeature.MainZoneMute or
            AvrFeature.MainZoneInput or
            AvrFeature.MainZoneStatus => capabilities.SupportsHttp || capabilities.SupportsTelnet == true,
            AvrFeature.LiveEvents or
            AvrFeature.ChannelLevelRead or
            AvrFeature.ChannelLevelControl or
            AvrFeature.SpeakerPresetControl or
            AvrFeature.SurroundModeControl or
            AvrFeature.DigitalInputModeControl => capabilities.SupportsTelnet == true,
            _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, null)
        };
    }

    /// <summary>
    /// Safely probes the Telnet control port and updates <see cref="ReceiverCapabilities"/>.
    /// <see cref="InitializeAsync"/> must have succeeded first so HTTP device information is retained.
    /// </summary>
    public async Task<DenonReceiverCapabilities> ProbeReceiverCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var capabilities = ReceiverCapabilities ?? throw new InvalidOperationException(
            "InitializeAsync muss vor dem Prüfen der Receiver-Fähigkeiten aufgerufen werden.");

        try
        {
            _ = await _telnetClient.SendCommandAsync("PW?", cancellationToken).ConfigureAwait(false);
            ReceiverCapabilities = capabilities with { SupportsTelnet = true };
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                          exception is IOException or SocketException or TaskCanceledException)
        {
            ReceiverCapabilities = capabilities with { SupportsTelnet = false };
        }

        return ReceiverCapabilities;
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

    /// <summary>Switches the Main Zone on using <see cref="PreferredControlProtocol"/>.</summary>
    public Task PowerOnAsync(CancellationToken cancellationToken = default) =>
        PowerOnAsync(PreferredControlProtocol, cancellationToken);

    /// <summary>Switches the Main Zone on through the selected transport.</summary>
    public Task PowerOnAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync(AvrFeature.MainZonePower, protocol,
            token => SendHttpCommandAsync(DenonEndpoints.PowerOn, token),
            token => _telnetClient.PowerOnAsync(token),
            cancellationToken);

    /// <summary>Switches the Main Zone to standby using <see cref="PreferredControlProtocol"/>.</summary>
    public Task PowerOffAsync(CancellationToken cancellationToken = default) =>
        PowerOffAsync(PreferredControlProtocol, cancellationToken);

    /// <summary>Switches the Main Zone to standby through the selected transport.</summary>
    public Task PowerOffAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync(AvrFeature.MainZonePower, protocol,
            token => SendHttpCommandAsync(DenonEndpoints.PowerStandby, token),
            token => _telnetClient.PowerOffAsync(token),
            cancellationToken);

    /// <summary>Raises the Main Zone volume by one receiver step using <see cref="PreferredControlProtocol"/>.</summary>
    public Task VolumeUpAsync(CancellationToken cancellationToken = default) =>
        VolumeUpAsync(PreferredControlProtocol, cancellationToken);

    /// <summary>Raises the Main Zone volume by one receiver step through the selected transport.</summary>
    public Task VolumeUpAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync(AvrFeature.MainZoneVolume, protocol,
            token => SendHttpCommandAsync(DenonEndpoints.VolumeUp, token),
            token => _telnetClient.VolumeUpAsync(token),
            cancellationToken);

    /// <summary>Lowers the Main Zone volume by one receiver step using <see cref="PreferredControlProtocol"/>.</summary>
    public Task VolumeDownAsync(CancellationToken cancellationToken = default) =>
        VolumeDownAsync(PreferredControlProtocol, cancellationToken);

    /// <summary>Lowers the Main Zone volume by one receiver step through the selected transport.</summary>
    public Task VolumeDownAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync(AvrFeature.MainZoneVolume, protocol,
            token => SendHttpCommandAsync(DenonEndpoints.VolumeDown, token),
            token => _telnetClient.VolumeDownAsync(token),
            cancellationToken);

    /// <summary>Sets the Main Zone volume, rounded to a half-decibel step.</summary>
    /// <param name="volumeDb">Volume from -80.0 through +18.0 dB.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public Task SetVolumeAsync(double volumeDb, CancellationToken cancellationToken = default) =>
        SetVolumeAsync(volumeDb, PreferredControlProtocol, cancellationToken);

    /// <summary>Sets Main Zone volume through the selected transport, rounded to a half-decibel step.</summary>
    public Task SetVolumeAsync(
        double volumeDb,
        DenonControlProtocol protocol,
        CancellationToken cancellationToken = default)
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
        return ExecuteControlAsync(AvrFeature.MainZoneVolume, protocol,
            token => SendHttpCommandAsync(DenonEndpoints.SetVolume(value), token),
            token => _telnetClient.SetVolumeAsync(roundedVolume, token),
            cancellationToken);
    }

    /// <summary>Enables or disables Main Zone muting.</summary>
    /// <param name="muted"><see langword="true"/> to mute; otherwise <see langword="false"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public Task SetMuteAsync(bool muted, CancellationToken cancellationToken = default) =>
        SetMuteAsync(muted, PreferredControlProtocol, cancellationToken);

    /// <summary>Enables or disables Main Zone muting through the selected transport.</summary>
    public Task SetMuteAsync(
        bool muted,
        DenonControlProtocol protocol,
        CancellationToken cancellationToken = default) =>
        ExecuteControlAsync(AvrFeature.MainZoneMute, protocol,
            token => SendHttpCommandAsync(muted ? DenonEndpoints.MuteOn : DenonEndpoints.MuteOff, token),
            token => _telnetClient.SetMuteAsync(muted, token),
            cancellationToken);

    /// <summary>Selects a Main Zone input using a display or Denon protocol name.</summary>
    /// <param name="input">Input such as <c>CBL/SAT</c>, <c>Media Player</c> or <c>MPLAY</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public Task SetInputAsync(string input, CancellationToken cancellationToken = default) =>
        SetInputAsync(input, PreferredControlProtocol, cancellationToken);

    /// <summary>Selects a Main Zone input through the selected transport.</summary>
    public Task SetInputAsync(
        string input,
        DenonControlProtocol protocol,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (input.Contains('\r') || input.Contains('\n'))
        {
            throw new ArgumentException("Der Eingangsname darf keinen Zeilenumbruch enthalten.", nameof(input));
        }

        var protocolName = DenonInputSource.ToProtocolName(input.Trim());
        return ExecuteControlAsync(AvrFeature.MainZoneInput, protocol,
            token => SendHttpCommandAsync(DenonEndpoints.SetInput(protocolName), token),
            token => _telnetClient.SetInputAsync(input, token),
            cancellationToken);
    }

    /// <summary>Reads one speaker channel level through Telnet.</summary>
    public Task<DenonSpeakerLevel> GetSpeakerLevelAsync(
        DenonSpeakerLevelChannel channel,
        DenonControlProtocol protocol = DenonControlProtocol.Auto,
        CancellationToken cancellationToken = default) =>
        ExecuteTelnetFeatureAsync(
            AvrFeature.ChannelLevelRead,
            protocol,
            token => _telnetClient.GetSpeakerLevelAsync(channel, token),
            cancellationToken);

    /// <summary>Reads all currently configured speaker channel levels through Telnet.</summary>
    public Task<IReadOnlyList<DenonSpeakerLevel>> GetSpeakerLevelsAsync(
        DenonControlProtocol protocol = DenonControlProtocol.Auto,
        CancellationToken cancellationToken = default) =>
        ExecuteTelnetFeatureAsync(
            AvrFeature.ChannelLevelRead,
            protocol,
            _telnetClient.GetSpeakerLevelsAsync,
            cancellationToken);

    /// <summary>Sets one speaker channel level through Telnet.</summary>
    public async Task SetSpeakerLevelAsync(
        DenonSpeakerLevelChannel channel,
        double decibels,
        DenonControlProtocol protocol = DenonControlProtocol.Auto,
        CancellationToken cancellationToken = default)
    {
        _ = await ExecuteTelnetFeatureAsync(
            AvrFeature.ChannelLevelControl,
            protocol,
            token => _telnetClient.SetSpeakerLevelAsync(channel, decibels, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Raises or lowers one speaker channel level through Telnet.</summary>
    public async Task ChangeSpeakerLevelAsync(
        DenonSpeakerLevelChannel channel,
        bool increase,
        DenonControlProtocol protocol = DenonControlProtocol.Auto,
        CancellationToken cancellationToken = default)
    {
        _ = await ExecuteTelnetFeatureAsync(
            AvrFeature.ChannelLevelControl,
            protocol,
            token => _telnetClient.ChangeSpeakerLevelAsync(channel, increase, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Switches one subwoofer channel level off through Telnet.</summary>
    public async Task SetSpeakerLevelOffAsync(
        DenonSpeakerLevelChannel channel,
        DenonControlProtocol protocol = DenonControlProtocol.Auto,
        CancellationToken cancellationToken = default)
    {
        _ = await ExecuteTelnetFeatureAsync(
            AvrFeature.ChannelLevelControl,
            protocol,
            token => _telnetClient.SetSpeakerLevelOffAsync(channel, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Resets all speaker channel levels to Denon's receiver factory defaults through Telnet.</summary>
    public async Task ResetSpeakerLevelsToFactoryDefaultsAsync(
        DenonControlProtocol protocol = DenonControlProtocol.Auto,
        CancellationToken cancellationToken = default)
    {
        _ = await ExecuteTelnetFeatureAsync(
            AvrFeature.ChannelLevelControl,
            protocol,
            _telnetClient.ResetSpeakerLevelsToFactoryDefaultsAsync,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets a speaker-preset level through the receiver-specific speaker configuration API.
    /// The index is the <c>Speaker index</c> used by that interface; values use 0.1 dB units.
    /// </summary>
    public Task SetSpeakerPresetLevelAsync(
        int speakerIndex,
        double decibels,
        CancellationToken cancellationToken = default)
    {
        if (speakerIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speakerIndex));
        }

        if (decibels is < -12.0 or > 12.0)
        {
            throw new ArgumentOutOfRangeException(nameof(decibels), "Der Pegel muss zwischen -12,0 und +12,0 dB liegen.");
        }

        return GetReceiverProfile().SpeakerPresetLevels.SetLevelAsync(
            new DenonProfileContext(Host, _httpTransport),
            speakerIndex,
            decibels,
            cancellationToken);
    }

    /// <summary>Reads the active speaker-preset levels using the selected receiver profile.</summary>
    public Task<IReadOnlyList<DenonSpeakerPresetLevel>> GetSpeakerPresetLevelsAsync(
        CancellationToken cancellationToken = default) =>
        GetReceiverProfile().SpeakerPresetLevels.GetLevelsAsync(
            new DenonProfileContext(Host, _httpTransport),
            cancellationToken);

    /// <summary>Sends a complete Denon HTTP command path.</summary>
    /// <param name="commandPath">Path beginning with <c>/</c>, including any query command.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task SendCommandAsync(
        string commandPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandPath);
        await SendHttpCommandAsync(commandPath, cancellationToken).ConfigureAwait(false);
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

    private async Task SendHttpCommandAsync(string commandPath, CancellationToken cancellationToken)
    {
        var port = GetInitializedPort();
        _ = await _httpTransport.GetStringAsync(Host, port, commandPath, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ExecuteControlAsync(
        AvrFeature feature,
        DenonControlProtocol protocol,
        Func<CancellationToken, Task> httpCommand,
        Func<CancellationToken, Task<string>> telnetCommand,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var supportedProtocols = GetSupportedProtocols(feature);

        switch (protocol)
        {
            case DenonControlProtocol.Http:
                EnsureProtocolSupportsFeature(DenonControlProtocol.Http, feature, supportedProtocols);
                await httpCommand(cancellationToken).ConfigureAwait(false);
                return;
            case DenonControlProtocol.Telnet:
                EnsureProtocolSupportsFeature(DenonControlProtocol.Telnet, feature, supportedProtocols);
                _ = await telnetCommand(cancellationToken).ConfigureAwait(false);
                return;
            case DenonControlProtocol.Auto:
                var useHttpFirst = HttpPort is not null && supportedProtocols.Contains(DenonControlProtocol.Http);
                if (!useHttpFirst)
                {
                    EnsureProtocolSupportsFeature(DenonControlProtocol.Telnet, feature, supportedProtocols);
                    _ = await telnetCommand(cancellationToken).ConfigureAwait(false);
                    return;
                }

                try
                {
                    await httpCommand(cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
                {
                    EnsureProtocolSupportsFeature(DenonControlProtocol.Telnet, feature, supportedProtocols);
                    _ = await telnetCommand(cancellationToken).ConfigureAwait(false);
                }

                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null);
        }
    }

    private Task<T> ExecuteTelnetFeatureAsync<T>(
        AvrFeature feature,
        DenonControlProtocol protocol,
        Func<CancellationToken, Task<T>> telnetCommand,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureProtocolSupportsFeature(
            DenonControlProtocol.Telnet,
            feature,
            GetSupportedProtocols(feature));

        if (protocol == DenonControlProtocol.Http)
        {
            throw new NotSupportedException($"{feature} wird über HTTP von DenonAvrNet nicht unterstützt.");
        }

        if (protocol is not (DenonControlProtocol.Auto or DenonControlProtocol.Telnet))
        {
            throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null);
        }

        return telnetCommand(cancellationToken);
    }

    private static void EnsureProtocolSupportsFeature(
        DenonControlProtocol protocol,
        AvrFeature feature,
        IReadOnlySet<DenonControlProtocol> supportedProtocols)
    {
        if (!supportedProtocols.Contains(protocol))
        {
            throw new NotSupportedException(
                $"{feature} wird über {protocol} von DenonAvrNet nicht unterstützt.");
        }
    }

    private static DenonReceiverCapabilities CreateReceiverCapabilities(
        DenonDeviceInfo deviceInfo,
        int httpPort)
    {
        var zoneCount = deviceInfo.ZoneCount;
        return new DenonReceiverCapabilities(
            SupportsHttp: true,
            SupportsTelnet: null,
            SupportsAppCommand: httpPort == 8080,
            SupportsAppCommand0300: null,
            SupportsZone2: zoneCount is >= 2,
            SupportsZone3: zoneCount is >= 3,
            ZoneCount: zoneCount);
    }

    private IDenonReceiverProfile GetReceiverProfile()
    {
        ThrowIfDisposed();
        return _receiverProfile ?? throw new InvalidOperationException(
            "InitializeAsync muss vor dem Abruf receiver-spezifischer Funktionen aufgerufen werden.");
    }

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
