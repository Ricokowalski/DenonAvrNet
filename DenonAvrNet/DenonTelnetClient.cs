using System.Net.Sockets;
using System.Text;
using DenonAvrNet.Models;
using DenonAvrNet.Protocol;

namespace DenonAvrNet;

/// <summary>Provides Denon's TCP/IP control protocol, normally available on port 23.</summary>
public sealed class DenonTelnetClient
{
    private readonly TimeSpan _timeout;

    /// <summary>Creates a Telnet protocol client for a receiver host.</summary>
    public DenonTelnetClient(string host, TimeSpan? requestTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        Host = host.Trim().Trim('[', ']');
        _timeout = requestTimeout ?? TimeSpan.FromSeconds(3);
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        }
    }

    /// <summary>Gets the receiver hostname or IP address.</summary>
    public string Host { get; }

    /// <summary>Sends one Denon TCP command and returns its first response line.</summary>
    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (command.Contains('\r') || command.Contains('\n'))
        {
            throw new ArgumentException("Ein Telnet-Befehl darf keinen Zeilenumbruch enthalten.", nameof(command));
        }

        using var timeoutSource = new CancellationTokenSource(_timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);
        var token = linkedSource.Token;
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(Host, 23, token).ConfigureAwait(false);
        await using var stream = tcpClient.GetStream();
        var request = Encoding.ASCII.GetBytes($"{command}\r");
        await stream.WriteAsync(request, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);

        var response = new StringBuilder();
        var buffer = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            var character = (char)buffer[0];
            if (character is '\r' or '\n')
            {
                if (response.Length > 0)
                {
                    break;
                }

                continue;
            }

            response.Append(character);
        }

        if (response.Length == 0)
        {
            throw new IOException("Der Receiver hat auf den Telnet-Befehl keine Antwort gesendet.");
        }

        return response.ToString();
    }

    /// <summary>Queries the current Zone 2 state.</summary>
    public Task<string> QueryZone2Async(CancellationToken cancellationToken = default) =>
        SendCommandAsync("Z2?", cancellationToken);

    /// <summary>Queries the current Zone 3 state.</summary>
    public Task<string> QueryZone3Async(CancellationToken cancellationToken = default) =>
        SendCommandAsync("Z3?", cancellationToken);

    /// <summary>Queries the Main Zone power state.</summary>
    public Task<string> QueryPowerAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("PW?", cancellationToken);

    /// <summary>Queries the Main Zone master volume.</summary>
    public Task<string> QueryVolumeAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("MV?", cancellationToken);

    /// <summary>Queries the Main Zone mute state.</summary>
    public Task<string> QueryMuteAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("MU?", cancellationToken);

    /// <summary>Queries the selected Main Zone input.</summary>
    public Task<string> QueryInputAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("SI?", cancellationToken);

    /// <summary>Queries the current surround mode.</summary>
    public Task<string> QuerySurroundModeAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("MS?", cancellationToken);

    /// <summary>Queries the configured digital input decoder mode.</summary>
    public Task<string> QueryDigitalInputModeAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("DC?", cancellationToken);

    /// <summary>Switches the Main Zone on.</summary>
    public Task<string> PowerOnAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("PWON", cancellationToken);

    /// <summary>Switches the Main Zone to standby.</summary>
    public Task<string> PowerOffAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("PWOFF", cancellationToken);

    /// <summary>Raises the Main Zone volume by one receiver step.</summary>
    public Task<string> VolumeUpAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("MVUP", cancellationToken);

    /// <summary>Lowers the Main Zone volume by one receiver step.</summary>
    public Task<string> VolumeDownAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("MVDOWN", cancellationToken);

    /// <summary>Sets Main Zone volume from -80.0 through +18.0 dB.</summary>
    public Task<string> SetVolumeAsync(double volumeDb, CancellationToken cancellationToken = default) =>
        SendCommandAsync($"MV{ToTelnetVolumeValue(volumeDb)}", cancellationToken);

    /// <summary>Enables or disables Main Zone muting.</summary>
    public Task<string> SetMuteAsync(bool muted, CancellationToken cancellationToken = default) =>
        SendCommandAsync(muted ? "MUON" : "MUOFF", cancellationToken);

    /// <summary>Selects a Main Zone input using a display or Denon protocol name.</summary>
    public Task<string> SetInputAsync(string input, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        if (input.Contains('\r') || input.Contains('\n'))
        {
            throw new ArgumentException("Der Eingangsname darf keinen Zeilenumbruch enthalten.", nameof(input));
        }

        return SendCommandAsync($"SI{DenonInputSource.ToProtocolName(input.Trim())}", cancellationToken);
    }

    /// <summary>Switches Zone 2 on or off.</summary>
    public Task<string> SetZone2PowerAsync(bool on, CancellationToken cancellationToken = default) =>
        SendCommandAsync(on ? "Z2ON" : "Z2OFF", cancellationToken);

    /// <summary>Switches Zone 3 on or off.</summary>
    public Task<string> SetZone3PowerAsync(bool on, CancellationToken cancellationToken = default) =>
        SendCommandAsync(on ? "Z3ON" : "Z3OFF", cancellationToken);

    /// <summary>Raises or lowers Zone 2 volume by one receiver step.</summary>
    public Task<string> ChangeZone2VolumeAsync(bool increase, CancellationToken cancellationToken = default) =>
        SendCommandAsync(increase ? "Z2UP" : "Z2DOWN", cancellationToken);

    /// <summary>Raises or lowers Zone 3 volume by one receiver step.</summary>
    public Task<string> ChangeZone3VolumeAsync(bool increase, CancellationToken cancellationToken = default) =>
        SendCommandAsync(increase ? "Z3UP" : "Z3DOWN", cancellationToken);

    /// <summary>Sets Zone 2 volume from -80.0 through +18.0 dB.</summary>
    public Task<string> SetZone2VolumeAsync(double volumeDb, CancellationToken cancellationToken = default) =>
        SendCommandAsync($"Z2{ToTelnetVolumeValue(volumeDb)}", cancellationToken);

    /// <summary>Sets Zone 3 volume from -80.0 through +18.0 dB.</summary>
    public Task<string> SetZone3VolumeAsync(double volumeDb, CancellationToken cancellationToken = default) =>
        SendCommandAsync($"Z3{ToTelnetVolumeValue(volumeDb)}", cancellationToken);

    /// <summary>Enables or disables Zone 2 muting.</summary>
    public Task<string> SetZone2MuteAsync(bool muted, CancellationToken cancellationToken = default) =>
        SendCommandAsync(muted ? "Z2MUON" : "Z2MUOFF", cancellationToken);

    /// <summary>Enables or disables Zone 3 muting.</summary>
    public Task<string> SetZone3MuteAsync(bool muted, CancellationToken cancellationToken = default) =>
        SendCommandAsync(muted ? "Z3MUON" : "Z3MUOFF", cancellationToken);

    /// <summary>Selects a Zone 2 input using a display or Denon protocol name.</summary>
    public Task<string> SetZone2InputAsync(string input, CancellationToken cancellationToken = default) =>
        SetZoneInputAsync("Z2", input, cancellationToken);

    /// <summary>Selects a Zone 3 input using a display or Denon protocol name.</summary>
    public Task<string> SetZone3InputAsync(string input, CancellationToken cancellationToken = default) =>
        SetZoneInputAsync("Z3", input, cancellationToken);

    /// <summary>Switches the receiver's configured Speaker Preset 1 or 2.</summary>
    public Task<string> SelectSpeakerPresetAsync(int preset, CancellationToken cancellationToken = default)
    {
        if (preset is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(preset),
                preset,
                "Der Receiver unterstützt Speaker Preset 1 oder 2.");
        }

        return SendCommandAsync($"SPPR {preset}", cancellationToken);
    }

    /// <summary>
    /// Sets a surround mode, for example <c>Dolby Surround</c>, <c>DTS Neural:X</c>,
    /// <c>Stereo</c>, <c>Multi Ch Stereo</c>, <c>Pure Direct</c> or <c>Auto</c>.
    /// </summary>
    public Task<string> SetSurroundModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        if (mode.Contains('\r') || mode.Contains('\n'))
        {
            throw new ArgumentException("Der Soundmodus darf keinen Zeilenumbruch enthalten.", nameof(mode));
        }

        var protocolMode = mode.Trim().ToUpperInvariant() switch
        {
            "AUTO" => "AUTO",
            "STEREO" => "STEREO",
            "PURE DIRECT" => "PURE DIRECT",
            "DOLBY SURROUND" => "DOLBY SURROUND",
            "DTS NEURAL:X" => "DTS NEURAL:X",
            "MULTI CH STEREO" or "MCH STEREO" => "MCH STEREO",
            _ => mode.Trim()
        };

        return SendCommandAsync($"MS{protocolMode}", cancellationToken);
    }

    /// <summary>Sets the digital input decoder to <c>Auto</c>, <c>PCM</c> or <c>DTS</c>.</summary>
    public Task<string> SetDigitalInputModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);

        var protocolMode = mode.Trim().ToUpperInvariant() switch
        {
            "AUTO" => "AUTO",
            "PCM" => "PCM",
            "DTS" => "DTS",
            _ => throw new ArgumentException(
                "Als digitaler Eingangsmodus sind nur Auto, PCM oder DTS zulässig.",
                nameof(mode))
        };

        return SendCommandAsync($"DC{protocolMode}", cancellationToken);
    }

    /// <summary>Reads one channel level through its specific Denon CV command.</summary>
    public async Task<DenonSpeakerLevel> GetSpeakerLevelAsync(
        DenonSpeakerLevelChannel channel,
        CancellationToken cancellationToken = default)
    {
        var code = DenonSpeakerLevelChannelConverter.ToProtocolCode(channel);
        var response = await SendCommandAsync($"CV{code}?", cancellationToken).ConfigureAwait(false);
        return ParseSpeakerLevel(response);
    }

    /// <summary>
    /// Reads all channel levels configured on the receiver. The receiver terminates the
    /// multi-line response with <c>CVEND</c>; absent/unconfigured channels are not returned.
    /// </summary>
    public async Task<IReadOnlyList<DenonSpeakerLevel>> GetSpeakerLevelsAsync(
        CancellationToken cancellationToken = default)
    {
        var responses = await SendCommandUntilAsync("CV?", "CVEND", cancellationToken).ConfigureAwait(false);
        return responses
            .Where(response => response.StartsWith("CV", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(response, "CVEND", StringComparison.OrdinalIgnoreCase))
            .Select(ParseSpeakerLevel)
            .ToArray();
    }

    /// <summary>Sets a channel level from -12.0 through +12.0 dB in half-decibel steps.</summary>
    public Task<string> SetSpeakerLevelAsync(
        DenonSpeakerLevelChannel channel,
        double decibels,
        CancellationToken cancellationToken = default)
    {
        var code = DenonSpeakerLevelChannelConverter.ToProtocolCode(channel);
        return SendCommandAsync($"CV{code} {ToChannelLevelValue(decibels)}", cancellationToken);
    }

    /// <summary>Raises or lowers a channel level by one receiver step.</summary>
    public Task<string> ChangeSpeakerLevelAsync(
        DenonSpeakerLevelChannel channel,
        bool increase,
        CancellationToken cancellationToken = default)
    {
        var code = DenonSpeakerLevelChannelConverter.ToProtocolCode(channel);
        return SendCommandAsync($"CV{code} {(increase ? "UP" : "DOWN")}", cancellationToken);
    }

    /// <summary>Switches a subwoofer channel level off. Other channels may be rejected by the receiver.</summary>
    public Task<string> SetSpeakerLevelOffAsync(
        DenonSpeakerLevelChannel channel,
        CancellationToken cancellationToken = default)
    {
        if (channel is not (DenonSpeakerLevelChannel.Subwoofer or
                            DenonSpeakerLevelChannel.Subwoofer2 or
                            DenonSpeakerLevelChannel.Subwoofer3 or
                            DenonSpeakerLevelChannel.Subwoofer4))
        {
            throw new ArgumentException("Nur Subwoofer-Kanäle können auf OFF gesetzt werden.", nameof(channel));
        }

        var code = DenonSpeakerLevelChannelConverter.ToProtocolCode(channel);
        return SendCommandAsync($"CV{code} 00", cancellationToken);
    }

    /// <summary>Resets all channel levels to Denon's receiver factory defaults.</summary>
    public Task<string> ResetSpeakerLevelsToFactoryDefaultsAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("CVZRL", cancellationToken);

    private Task<string> SetZoneInputAsync(
        string zonePrefix,
        string input,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        if (input.Contains('\r') || input.Contains('\n'))
        {
            throw new ArgumentException("Der Eingangsname darf keinen Zeilenumbruch enthalten.", nameof(input));
        }

        var protocolName = DenonInputSource.ToProtocolName(input.Trim());
        return SendCommandAsync($"{zonePrefix}{protocolName}", cancellationToken);
    }

    private static string ToTelnetVolumeValue(double volumeDb)
    {
        if (volumeDb is < -80.0 or > 18.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(volumeDb),
                volumeDb,
                "Die Lautstärke muss zwischen -80,0 und +18,0 dB liegen.");
        }

        var roundedVolume = Math.Round(volumeDb * 2, MidpointRounding.ToEven) / 2.0;
        var protocolValue = roundedVolume + 80.0;
        var wholeValue = (int)Math.Floor(protocolValue);

        return protocolValue - wholeValue >= 0.5
            ? $"{wholeValue:00}5"
            : $"{wholeValue:00}";
    }

    private async Task<IReadOnlyList<string>> SendCommandUntilAsync(
        string command,
        string terminatingResponse,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(_timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);
        var token = linkedSource.Token;
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(Host, 23, token).ConfigureAwait(false);
        await using var stream = tcpClient.GetStream();
        var request = Encoding.ASCII.GetBytes($"{command}\r");
        await stream.WriteAsync(request, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);

        var responses = new List<string>();
        var line = new StringBuilder();
        var buffer = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            var character = (char)buffer[0];
            if (character is not ('\r' or '\n'))
            {
                line.Append(character);
                continue;
            }

            if (line.Length == 0)
            {
                continue;
            }

            var response = line.ToString();
            responses.Add(response);
            if (string.Equals(response, terminatingResponse, StringComparison.OrdinalIgnoreCase))
            {
                return responses;
            }

            line.Clear();
        }

        throw new IOException($"Der Receiver hat die erwartete Abschlussmeldung '{terminatingResponse}' nicht gesendet.");
    }

    private static DenonSpeakerLevel ParseSpeakerLevel(string response)
    {
        if (!response.StartsWith("CV", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"Keine CV-Antwort: '{response}'.");
        }

        var parts = response[2..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !DenonSpeakerLevelChannelConverter.TryFromProtocolCode(parts[0], out var channel))
        {
            throw new FormatException($"Unbekannte CV-Antwort: '{response}'.");
        }

        if (parts[1] == "00")
        {
            return new DenonSpeakerLevel(channel, null, true, response);
        }

        if (!int.TryParse(parts[1], out var encodedLevel))
        {
            throw new FormatException($"Ungültiger Kanalpegel in '{response}'.");
        }

        var decibels = parts[1].Length == 3 ? encodedLevel / 10.0 - 50.0 : encodedLevel - 50.0;
        return new DenonSpeakerLevel(channel, decibels, false, response);
    }

    private static string ToChannelLevelValue(double decibels)
    {
        if (decibels is < -12.0 or > 12.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decibels), decibels, "Der Kanalpegel muss zwischen -12,0 und +12,0 dB liegen.");
        }

        var roundedLevel = Math.Round(decibels * 2, MidpointRounding.ToEven) / 2.0;
        var encodedLevel = roundedLevel + 50.0;
        var wholeValue = (int)Math.Floor(encodedLevel);
        return encodedLevel - wholeValue >= 0.5 ? $"{wholeValue:00}5" : $"{wholeValue:00}";
    }
}
