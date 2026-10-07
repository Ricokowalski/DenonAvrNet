using DenonAvrNet;
using DenonAvrNet.Exceptions;
using DenonAvrNet.Logger;
using DenonAvrNet.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

Console.Write("Receiver IP [10.37.0.23]: ");
var enteredHost = Console.ReadLine()?.Trim();
var host = string.IsNullOrWhiteSpace(enteredHost) ? "10.37.0.23" : enteredHost;

using var receiver = new DenonAvrClient(host);

try
{
    Console.WriteLine($"\nConnecting to {host} …");
    var device = await receiver.InitializeAsync(cancellationSource.Token);
    Console.WriteLine("Connection established:");
    Console.WriteLine($"  Model:       {device.ModelName}");
    Console.WriteLine($"  API Version: {device.CommunicationApiVersion ?? "unknown"}");
    Console.WriteLine($"  Zones:       {device.ZoneCount?.ToString() ?? "unknown"}");
    Console.WriteLine($"  HTTP Port:   {receiver.HttpPort}");

    var telnetOutputEnabled = true;

    await using var monitor = new DenonReceiverMonitor(host);
    monitor.TelnetEventReceived += telnetEvent =>
    {
        if (!telnetOutputEnabled)
        {
            return;
            // <-----------
        }

        if (telnetEvent.ActiveSpeakerMatrix is { } matrix)
        {
            Console.WriteLine(
                $"\n[Telnet {telnetEvent.Timestamp:HH:mm:ss}] Active speakers: " +
                $"{matrix.ActivePositionCount}/{matrix.PositionCount} output positions " +
                $"(Matrix: {matrix.RawValues})");
            return;
        }
        Console.WriteLine($"\n[Telnet {telnetEvent.Timestamp:HH:mm:ss}] {telnetEvent.Message}");
    };

    monitor.Error += exception =>
        Console.Error.WriteLine($"\n[Monitor] {exception.Message}");

    await monitor.StartAsync(cancellationSource.Token);
    Console.WriteLine("Background monitor active (Telnet events + status refresh every 15 seconds).");

    await ShowStatusAsync(receiver, cancellationSource.Token);

    while (!cancellationSource.IsCancellationRequested)
    {
        PrintMenu(telnetOutputEnabled);

        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        try
        {
            switch (choice)
            {
                case "1":
                    await ShowStatusAsync(receiver, cancellationSource.Token);
                    break;
                case "2":
                    await ExecuteAndRefreshAsync(receiver, receiver.PowerOnAsync, cancellationSource.Token);
                    break;
                case "3":
                    if (Confirm("Really switch Main Zone to standby?"))
                    {
                        await receiver.PowerOffAsync(cancellationSource.Token);
                        Console.WriteLine("Standby command sent.");
                    }
                    break;
                case "4":
                    await ExecuteAndRefreshAsync(receiver, receiver.VolumeUpAsync, cancellationSource.Token);
                    break;
                case "5":
                    await ExecuteAndRefreshAsync(receiver, receiver.VolumeDownAsync, cancellationSource.Token);
                    break;
                case "6":
                    await SetVolumeAsync(receiver, cancellationSource.Token);
                    break;
                case "7":
                    await ExecuteAndRefreshAsync(
                        receiver,
                        token => receiver.SetMuteAsync(true, token),
                        cancellationSource.Token);
                    break;
                case "8":
                    await ExecuteAndRefreshAsync(
                        receiver,
                        token => receiver.SetMuteAsync(false, token),
                        cancellationSource.Token);
                    break;
                case "9":
                    await SetInputAsync(receiver, cancellationSource.Token);
                    break;
                case "D":
                    await RunReadOnlyDiagnosticAsync(receiver, cancellationSource.Token);
                    break;
                case "Z":
                    await ControlAdditionalZonesAsync(host, receiver, cancellationSource.Token);
                    break;
                // <-----------
                case "A":
                    await ControlAudioAndSpeakerPresetsAsync(host, receiver, cancellationSource.Token);
                    break;
                // <-----------
                case "P":
                    await ControlSpeakerPresetHttpAsync(receiver, cancellationSource.Token);
                    break;
                // <-----------
                case "L":
                    await ControlSpeakerLevelsAsync(receiver, cancellationSource.Token);
                    break;
                // <-----------
                case "S":
                    await ShowSpeakerLevelsAndDistancesAsync(receiver, cancellationSource.Token);
                    break;
                // <-----------
                case "G":
                    ControlReceiverLogger();
                    break;
                // <-----------
                case "TE":
                    telnetOutputEnabled = true;
                    Console.WriteLine("Telnet output enabled.");
                    break;
                // <-----------
                case "TD":
                    telnetOutputEnabled = false;
                    Console.WriteLine("Telnet output disabled.");
                    break;
                // <-----------
                case "LE":
                    ReceiverLogger.Enabled = true;
                    Console.WriteLine($"Logging enabled ({ReceiverLogger.FilePath}).");
                    break;
                // <-----------
                case "LD":
                    ReceiverLogger.Enabled = false;
                    Console.WriteLine("Logging disabled.");
                    break;
                // <-----------
                case "LTE":
                    ReceiverLogger.TelnetLoggingEnabled = true;
                    Console.WriteLine("Telnet logging enabled.");
                    break;
                // <-----------
                case "LTD":
                    ReceiverLogger.TelnetLoggingEnabled = false;
                    Console.WriteLine("Telnet logging disabled.");
                    break;
                // <-----------
                case "0":
                case "Q":
                    return;
                default:
                    Console.WriteLine("Unknown selection.");
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            throw;
            // <-----------
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("\nTimeout: The receiver did not respond in time. Program continues.");
        }
        catch (DenonAvrException exception)
        {
            Console.Error.WriteLine($"\nDenon error: {exception.Message}");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
            IOException or
            System.Net.Sockets.SocketException or
            NotSupportedException or
            InvalidOperationException or
            ArgumentException or
            FormatException or
            InvalidDataException)
        {
            Console.Error.WriteLine($"\nError: {exception.Message}");
        }
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nCancelled.");
}
catch (DenonAvrException exception)
{
    Console.Error.WriteLine($"\nDenon error: {exception.Message}");
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine($"\nNetwork error: {exception.Message}");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"\nUnexpected error: {exception}");
}

static void PrintMenu(bool telnetOutputEnabled)
{
    Console.WriteLine($"""

        ── Denon AVR Test Program ──

        STATUS
          1    Refresh status
          D    Read-only diagnostic (5 status queries)

        MAIN ZONE
          2    Power on
          3    Standby
          4    Volume up
          5    Volume down
          6    Set volume
          7    Mute on
          8    Mute off
          9    Select input

        ZONES AND AUDIO
          Z    Zone 2/3: power, volume, mute, input (Telnet)
          A    Speaker presets 1/2 and audio modes (Telnet)

        SPEAKERS
          P    Speaker preset: read/switch (HTTP, with confirmation)
          L    Channel levels (Telnet)
          S    Show speaker levels and distances (HTTP)

        CONSOLE AND LOG
          TE   Telnet output on      TD   Telnet output off     (now: {(telnetOutputEnabled ? "on" : "off")})
          LE   Log on                LD   Log off               (now: {(ReceiverLogger.Enabled ? "on" : "off")})
          LTE  Log Telnet on         LTD  Log Telnet off        (now: {(ReceiverLogger.TelnetLoggingEnabled ? "on" : "off")})
          G    Log setup (path, enable/disable, clear)

          0    Exit (also Q)
        """);

    Console.Write("Selection: ");
}

static async Task ControlSpeakerLevelsAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        Console.WriteLine("""
            ── Speaker Channel Levels (Telnet) ──
            1  Show all speaker preset levels (HTTP)
            2  Set channel level directly
            3  Increase channel level incrementally
            4  Decrease channel level incrementally
            5  Set subwoofer channel to OFF
            H  Set speaker preset level via HTTP (actual setup values)
            W  Reset all channel levels to Denon factory defaults
            0  Back to main menu
            """);
        Console.Write("Level selection: ");
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        switch (choice)
        {
            case "0":
            case "":
            case null:
                return;
            case "1":
                await ShowSpeakerLevelsAsync(receiver, cancellationToken);
                break;
            case "2":
                await SetSpeakerLevelFromConsoleAsync(receiver, cancellationToken);
                break;
            case "3":
                await ChangeSpeakerLevelFromConsoleAsync(receiver, true, cancellationToken);
                break;
            case "4":
                await ChangeSpeakerLevelFromConsoleAsync(receiver, false, cancellationToken);
                break;
            case "5":
                await SetSubwooferLevelOffFromConsoleAsync(receiver, cancellationToken);
                break;
            case "H":
                await SetSpeakerPresetLevelFromConsoleAsync(receiver, cancellationToken);
                break;
            case "W":
                if (Confirm("Really reset all channel levels to Denon factory defaults?"))
                {
                    await receiver.ResetSpeakerLevelsToFactoryDefaultsAsync(
                        DenonControlProtocol.Telnet,
                        cancellationToken);
                    Console.WriteLine("Channel levels have been reset to Denon factory defaults.");
                }
                break;
            default:
                Console.WriteLine("Invalid selection.");
                break;
        }
    }
}

static async Task SetSpeakerPresetLevelFromConsoleAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    Console.Write("Speaker index from web interface: ");
    if (!int.TryParse(Console.ReadLine()?.Trim(), out var speakerIndex) || speakerIndex < 0)
    {
        Console.WriteLine("Invalid speaker index.");
        return;
    }

    Console.Write("New speaker preset level (-12.0 to +12.0 dB): ");
    if (!TryParseGermanOrInvariantDouble(Console.ReadLine()?.Trim(), out var decibels))
    {
        Console.WriteLine("Invalid level.");
        return;
    }

    await receiver.SetSpeakerPresetLevelAsync(speakerIndex, decibels, cancellationToken);
    Console.WriteLine($"Speaker index {speakerIndex}: {decibels:0.0} dB set in speaker preset.");
}

static async Task ShowSpeakerLevelsAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    var levels = await receiver.GetSpeakerPresetLevelsAsync(cancellationToken);
    if (levels.Count == 0)
    {
        Console.WriteLine("The receiver returned no channel levels.");
        return;
    }

    Console.WriteLine("Active speaker preset levels:");
    foreach (var level in levels)
    {
        Console.WriteLine($"  {(level.Channel?.ToString() ?? "Unknown"),-42} {level.Decibels:0.0} dB (Index {level.SpeakerIndex})");
    }
}

static async Task ShowSpeakerLevelsAndDistancesAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    if (!receiver.IsFeatureAvailable(AvrFeature.SpeakerPresetLevelControl) ||
        !receiver.IsFeatureAvailable(AvrFeature.SpeakerDistanceControl))
    {
        Console.WriteLine("This receiver does not support reading speaker levels and distances via HTTP.");
        return;
        // <-----------
    }

    var speakers = await receiver.GetConfiguredSpeakersAsync(cancellationToken);

    if (speakers.Count == 0)
    {
        Console.WriteLine("The receiver reported no configured speakers.");
        return;
        // <-----------
    }

    Console.WriteLine("Speaker levels and distances (active speaker preset):");
    Console.WriteLine($"  {"Index",5}  {"Channel",-24} {"Level",10}  {"Distance",10}");

    foreach (var speaker in speakers)
    {
        var levelText = speaker.LevelDb is null
            ? "-"
            : FormatVolume(speaker.LevelDb);
        var distanceText = speaker.DistanceMeters is { } meters
            ? $"{meters.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"))} m"
            : "-";

        Console.WriteLine($"  {speaker.SpeakerIndex,5}  {speaker.Channel,-24} {levelText,10}  {distanceText,10}");
    }
}

static async Task SetSpeakerLevelFromConsoleAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    var channel = await SelectSpeakerLevelChannelAsync(receiver, false, cancellationToken);
    if (channel is null)
    {
        return;
    }

    Console.Write("New level (-12.0 to +12.0 dB, steps of 0.5): ");
    if (!TryParseGermanOrInvariantDouble(Console.ReadLine()?.Trim(), out var decibels))
    {
        Console.WriteLine("Invalid level.");
        return;
    }

    await receiver.SetSpeakerLevelAsync(channel.Value, decibels, DenonControlProtocol.Telnet, cancellationToken);
    Console.WriteLine($"{channel.Value}: {decibels:0.0} dB set.");
}

static async Task ChangeSpeakerLevelFromConsoleAsync(
    DenonAvrClient receiver,
    bool increase,
    CancellationToken cancellationToken)
{
    var channel = await SelectSpeakerLevelChannelAsync(receiver, false, cancellationToken);
    if (channel is null)
    {
        return;
    }

    await receiver.ChangeSpeakerLevelAsync(
        channel.Value,
        increase,
        DenonControlProtocol.Telnet,
        cancellationToken);
    Console.WriteLine($"{channel.Value}: Level {(increase ? "increased" : "decreased")}.");
}

static async Task SetSubwooferLevelOffFromConsoleAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    var channel = await SelectSpeakerLevelChannelAsync(receiver, true, cancellationToken);
    if (channel is null)
    {
        return;
    }

    await receiver.SetSpeakerLevelOffAsync(channel.Value, DenonControlProtocol.Telnet, cancellationToken);
    Console.WriteLine($"{channel.Value}: OFF set.");
}

static async Task<DenonSpeakerLevelChannel?> SelectSpeakerLevelChannelAsync(
    DenonAvrClient receiver,
    bool subwoofersOnly,
    CancellationToken cancellationToken)
{
    var levels = await receiver.GetSpeakerLevelsAsync(DenonControlProtocol.Telnet, cancellationToken);
    var selectableLevels = levels
        .Where(level => !subwoofersOnly || IsSubwooferChannel(level.Channel))
        .OrderBy(level => level.Channel)
        .ToArray();

    if (selectableLevels.Length == 0)
    {
        Console.WriteLine(subwoofersOnly
            ? "No configured subwoofer channel is available."
            : "The receiver returned no configured channel levels.");
        return null;
    }

    Console.WriteLine("Select channel:");
    for (var index = 0; index < selectableLevels.Length; index++)
    {
        var level = selectableLevels[index];
        Console.WriteLine($"  {index + 1,2}  {level.Channel,-24} {FormatSpeakerLevel(level)}");
    }

    Console.Write("Number: ");
    return int.TryParse(Console.ReadLine()?.Trim(), out var selection) &&
           selection >= 1 && selection <= selectableLevels.Length
        ? selectableLevels[selection - 1].Channel
        : null;
}

static bool IsSubwooferChannel(DenonSpeakerLevelChannel channel) => channel is
    DenonSpeakerLevelChannel.Subwoofer or
    DenonSpeakerLevelChannel.Subwoofer2 or
    DenonSpeakerLevelChannel.Subwoofer3 or
    DenonSpeakerLevelChannel.Subwoofer4;

static string FormatSpeakerLevel(DenonSpeakerLevel level) => level.IsOff
    ? "OFF"
    : level.Decibels is { } decibels
        ? $"{decibels.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"))} dB"
        : "unknown";

static async Task ControlAdditionalZonesAsync(
    string host,
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    var telnet = new DenonTelnetClient(host);
    while (!cancellationToken.IsCancellationRequested)
    {
        // Z2?/Z3? can return the stored source (for example Z3SOURCE). That is
        // not a power response. The HTTP snapshot contains both independent
        // values and is therefore authoritative for the displayed state.
        await ShowAdditionalZonesAsync(receiver, cancellationToken);
        Console.WriteLine("""
            1  Switch Zone 2 on         2  Switch Zone 2 to standby
            3  Switch Zone 3 on         4  Switch Zone 3 to standby
            5  Zone 2 volume up         6  Zone 2 volume down
            7  Zone 3 volume up         8  Zone 3 volume down
            V  Set volume directly
            M  Toggle mute
            E  Select input
            0  Back to main menu
            """);
        Console.Write("Zone selection: ");
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        switch (choice)
        {
            case "0":
            case "":
            case null:
                return;
            case "1":
                await telnet.SetZone2PowerAsync(true, cancellationToken);
                break;
            case "2":
                await telnet.SetZone2PowerAsync(false, cancellationToken);
                break;
            case "3":
                await telnet.SetZone3PowerAsync(true, cancellationToken);
                break;
            case "4":
                await telnet.SetZone3PowerAsync(false, cancellationToken);
                break;
            case "5":
                await telnet.ChangeZone2VolumeAsync(true, cancellationToken);
                break;
            case "6":
                await telnet.ChangeZone2VolumeAsync(false, cancellationToken);
                break;
            case "7":
                await telnet.ChangeZone3VolumeAsync(true, cancellationToken);
                break;
            case "8":
                await telnet.ChangeZone3VolumeAsync(false, cancellationToken);
                break;
            case "V":
                await SetAdditionalZoneVolumeAsync(telnet, cancellationToken);
                break;
            case "M":
                await SetAdditionalZoneMuteAsync(telnet, cancellationToken);
                break;
            case "E":
                await SetAdditionalZoneInputAsync(telnet, receiver, cancellationToken);
                break;
            default:
                Console.WriteLine("Invalid selection.");
                continue;
        }
        await Task.Delay(350, cancellationToken);
    }
}

static async Task ControlAudioAndSpeakerPresetsAsync(
    string host,
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    var telnet = new DenonTelnetClient(host);
    // A speaker preset switch can take up to about 15 seconds on the AVC-X6800H.
    var presetTelnet = new DenonTelnetClient(host, TimeSpan.FromSeconds(30));
    while (!cancellationToken.IsCancellationRequested)
    {
        Console.WriteLine("""
            ── Speaker Presets and Audio ──
            1  Speaker Preset 1
            2  Speaker Preset 2
            3  Select surround/sound mode
            4  Select digital input decoder (Auto / PCM / DTS)
            0  Back to main menu
            """);
        Console.Write("Audio selection: ");
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        switch (choice)
        {
            case "0":
            case "":
            case null:
                return;
            case "1":
                await ExecuteAudioCommandAsync(
                    () => presetTelnet.SelectSpeakerPresetAsync(1, cancellationToken),
                    receiver,
                    cancellationToken,
                    800);
                break;
            // <-----------
            case "2":
                await ExecuteAudioCommandAsync(
                    () => presetTelnet.SelectSpeakerPresetAsync(2, cancellationToken),
                    receiver,
                    cancellationToken,
                    800);
                break;
            // <-----------
            case "3":
                await SetSurroundModeAsync(telnet, receiver, cancellationToken);
                break;
            case "4":
                await SetDigitalInputModeAsync(telnet, receiver, cancellationToken);
                break;
            default:
                Console.WriteLine("Invalid selection.");
                break;
        }
    }
}

static async Task ControlSpeakerPresetHttpAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    if (!receiver.IsFeatureAvailable(AvrFeature.SpeakerPresetSelection))
    {
        Console.WriteLine("This receiver does not support reading and switching the speaker preset via HTTP.");
        return;
        // <-----------
    }

    while (!cancellationToken.IsCancellationRequested)
    {
        Console.WriteLine("""
            ── Speaker Preset (HTTP, Port 11080) ──
            1  Read active preset
            2  Switch preset (waits for confirmation, then stops test tone)
            0  Back to main menu
            """);
        Console.Write("Preset selection: ");
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        switch (choice)
        {
            case "0":
            case "":
            case null:
                return;
            // <-----------
            case "1":
                await ShowActiveSpeakerPresetAsync(receiver, cancellationToken);
                break;
            // <-----------
            case "2":
                await SelectSpeakerPresetFromConsoleAsync(receiver, cancellationToken);
                break;
            // <-----------
            default:
                Console.WriteLine("Invalid selection.");
                break;
                // <-----------
        }
    }
}

static async Task ShowActiveSpeakerPresetAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    var stopwatch = Stopwatch.StartNew();
    var preset = await receiver.GetActiveSpeakerPresetAsync(cancellationToken);
    stopwatch.Stop();
    Console.WriteLine($"Active speaker preset: {preset} ({stopwatch.ElapsedMilliseconds} ms)");
}

static async Task SelectSpeakerPresetFromConsoleAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    Console.Write($"Target preset (1 to {receiver.SpeakerPresetCount}): ");
    if (!int.TryParse(Console.ReadLine()?.Trim(), out var preset) ||
        preset < 1 ||
        preset > receiver.SpeakerPresetCount)
    {
        Console.WriteLine("Invalid preset.");
        return;
        // <-----------
    }

    if (!Confirm("Switching changes the receiver, can take up to about 15 seconds and briefly interrupt audio. Continue?"))
    {
        return;
        // <-----------
    }

    Console.WriteLine($"Switching to preset {preset} …");
    var stopwatch = Stopwatch.StartNew();
    await receiver.SelectSpeakerPresetAsync(preset, cancellationToken: cancellationToken);
    stopwatch.Stop();
    Console.WriteLine($"Preset {preset} confirmed after {stopwatch.ElapsedMilliseconds} ms. Test tone stopped.");
}

static async Task SetSurroundModeAsync(
    DenonTelnetClient telnet,
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    Console.WriteLine("""
        1  Auto
        2  Stereo
        3  Dolby Surround
        4  DTS Neural:X
        5  Multi Ch Stereo
        6  Pure Direct
        """);
    Console.Write("Sound mode: ");
    var mode = Console.ReadLine()?.Trim() switch
    {
        "1" => "Auto",
        "2" => "Stereo",
        "3" => "Dolby Surround",
        "4" => "DTS Neural:X",
        "5" => "Multi Ch Stereo",
        "6" => "Pure Direct",
        _ => null
    };

    if (mode is null)
    {
        Console.WriteLine("Invalid selection.");
        return;
    }

    await ExecuteAudioCommandAsync(
        () => telnet.SetSurroundModeAsync(mode, cancellationToken),
        receiver,
        cancellationToken,
        500);
}

static async Task SetDigitalInputModeAsync(
    DenonTelnetClient telnet,
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    Console.Write("Digital input decoder [A]uto / [P]CM / [D]TS: ");
    var mode = Console.ReadLine()?.Trim().ToUpperInvariant() switch
    {
        "A" => "Auto",
        "P" => "PCM",
        "D" => "DTS",
        _ => null
    };

    if (mode is null)
    {
        Console.WriteLine("Please enter A, P or D.");
        return;
    }

    await ExecuteAudioCommandAsync(
        () => telnet.SetDigitalInputModeAsync(mode, cancellationToken),
        receiver,
        cancellationToken,
        350);
}

static async Task ExecuteAudioCommandAsync(
    Func<Task<string>> command,
    DenonAvrClient receiver,
    CancellationToken cancellationToken,
    int settleDelayMilliseconds)
{
    try
    {
        var response = await command();
        Console.WriteLine($"Receiver response: {response}");
        await Task.Delay(settleDelayMilliseconds, cancellationToken);
        await ShowStatusAsync(receiver, cancellationToken);
    }
    catch (ArgumentException exception)
    {
        Console.WriteLine(exception.Message);
    }
}

static async Task ShowAdditionalZonesAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    var state = await receiver.UpdateAsync(cancellationToken);
    Console.WriteLine("\nCurrent zone status:");
    ShowAdditionalZone("Zone 2", state.Zone2);
    ShowAdditionalZone("Zone 3", state.Zone3);
}

static async Task SetAdditionalZoneVolumeAsync(
    DenonTelnetClient telnet,
    CancellationToken cancellationToken)
{
    if (!TryReadZone(out var zone))
    {
        return;
    }

    Console.Write("Volume in dB (-80.0 to +18.0): ");
    if (!TryParseGermanOrInvariantDouble(Console.ReadLine(), out var volume))
    {
        Console.WriteLine("Invalid number.");
        return;
    }

    try
    {
        if (zone == 2)
        {
            await telnet.SetZone2VolumeAsync(volume, cancellationToken);
        }
        else
        {
            await telnet.SetZone3VolumeAsync(volume, cancellationToken);
        }
    }
    catch (ArgumentOutOfRangeException exception)
    {
        Console.WriteLine(exception.Message);
    }
}

static async Task SetAdditionalZoneMuteAsync(
    DenonTelnetClient telnet,
    CancellationToken cancellationToken)
{
    if (!TryReadZone(out var zone))
    {
        return;
    }

    Console.Write("Mute [E]nable/[D]isable: ");
    var selection = Console.ReadLine()?.Trim().ToUpperInvariant();
    if (selection is not ("E" or "D"))
    {
        Console.WriteLine("Please enter E or D.");
        return;
    }

    var muted = selection.Equals("E", StringComparison.OrdinalIgnoreCase);
    if (zone == 2)
    {
        await telnet.SetZone2MuteAsync(muted, cancellationToken);
    }
    else
    {
        await telnet.SetZone3MuteAsync(muted, cancellationToken);
    }
}

static async Task SetAdditionalZoneInputAsync(
    DenonTelnetClient telnet,
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    if (!TryReadZone(out var zone))
    {
        return;
    }

    var inputs = await receiver.RefreshInputsAsync(cancellationToken);
    Console.WriteLine("Available inputs:");
    for (var index = 0; index < inputs.Count; index++)
    {
        Console.WriteLine($"  {index + 1,2}: {inputs[index]}");
    }

    Console.Write("Number or Denon protocol name: ");
    var selection = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(selection))
    {
        return;
    }

    var input = int.TryParse(selection, out var number) &&
                number >= 1 && number <= inputs.Count
        ? inputs[number - 1]
        : selection;

    if (zone == 2)
    {
        await telnet.SetZone2InputAsync(input, cancellationToken);
    }
    else
    {
        await telnet.SetZone3InputAsync(input, cancellationToken);
    }
}

static bool TryReadZone(out int zone)
{
    Console.Write("Zone [2/3]: ");
    var text = Console.ReadLine()?.Trim();
    zone = text is "2" or "3" ? int.Parse(text) : 0;
    if (zone != 0)
    {
        return true;
    }

    Console.WriteLine("Please enter only 2 or 3.");
    return false;
}

static async Task ExecuteAndRefreshAsync(
    DenonAvrClient receiver,
    Func<CancellationToken, Task> command,
    CancellationToken cancellationToken)
{
    await command(cancellationToken);
    await Task.Delay(350, cancellationToken);
    await ShowStatusAsync(receiver, cancellationToken);
}

static async Task ShowStatusAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    var state = await receiver.UpdateAsync(cancellationToken);
    Console.WriteLine("\nCurrent Main Zone status:");
    Console.WriteLine($"  Power:       {state.Power}");
    Console.WriteLine($"  Input:       {state.Input ?? "unknown"}");
    Console.WriteLine($"  Volume:      {FormatVolume(state.VolumeDb)}");
    Console.WriteLine($"  Mute:        {FormatMute(state.IsMuted)}");
    Console.WriteLine($"  Audio input: {state.Audio?.InputMode ?? "unknown"}");
    Console.WriteLine($"  Audio format:{state.Audio?.AudioFormat ?? "unknown"}");
    Console.WriteLine($"  Sound mode:  {state.Audio?.SoundMode ?? "unknown"}");
    Console.WriteLine($"  Sample rate: {state.Audio?.SampleRate ?? "unknown"}");
    Console.WriteLine($"  Speakers:    {FormatSpeakers(state.Audio?.ActiveSpeakers)}");
    ShowAdditionalZone("Zone 2", state.Zone2);
    ShowAdditionalZone("Zone 3", state.Zone3);
}

static void ShowAdditionalZone(string name, DenonAvrNet.Models.DenonZoneState? zone)
{
    if (zone is not null)
    {
        Console.WriteLine($"  {name}:       {zone.Power}, {zone.Input ?? "unknown"}, {FormatVolume(zone.VolumeDb)}, Mute {FormatMute(zone.IsMuted)}");
    }
}

static async Task SetVolumeAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    Console.Write("Volume in dB (-80.0 to +18.0): ");
    var text = Console.ReadLine();
    if (!TryParseGermanOrInvariantDouble(text, out var volume))
    {
        Console.WriteLine("Invalid number.");
        return;
    }

    try
    {
        await ExecuteAndRefreshAsync(
            receiver,
            token => receiver.SetVolumeAsync(volume, token),
            cancellationToken);
    }
    catch (ArgumentOutOfRangeException exception)
    {
        Console.WriteLine(exception.Message);
    }
}

static async Task SetInputAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    var inputs = await receiver.RefreshInputsAsync(cancellationToken);
    Console.WriteLine("Available inputs:");
    for (var index = 0; index < inputs.Count; index++)
    {
        Console.WriteLine($"  {index + 1,2}: {inputs[index]}");
    }

    Console.Write("Number or Denon protocol name: ");
    var selection = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(selection))
    {
        return;
    }

    var input = int.TryParse(selection, out var number) &&
                number >= 1 && number <= inputs.Count
        ? inputs[number - 1]
        : selection;

    await ExecuteAndRefreshAsync(
        receiver,
        token => receiver.SetInputAsync(input, token),
        cancellationToken);
}

static async Task RunReadOnlyDiagnosticAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    const int repetitions = 5;
    Console.WriteLine(
        $"\nStarting {repetitions} read-only status queries; no control commands will be sent.");
    for (var attempt = 1; attempt <= repetitions; attempt++)
    {
        var stopwatch = Stopwatch.StartNew();
        var state = await receiver.UpdateAsync(cancellationToken);
        stopwatch.Stop();
        Console.WriteLine(
            $"  {attempt}/{repetitions}  {stopwatch.ElapsedMilliseconds,4} ms | " +
            $"Power {state.Power} | Input {state.Input ?? "?"} | " +
            $"Volume {FormatVolume(state.VolumeDb)} | " +
            $"Audio {state.Audio?.AudioFormat ?? "?"} | " +
            $"Speakers {FormatSpeakers(state.Audio?.ActiveSpeakers)}");
        if (attempt < repetitions)
        {
            await Task.Delay(500, cancellationToken);
        }
    }

    Console.WriteLine("Read-only diagnostic completed.");
}

static bool Confirm(string question)
{
    Console.Write($"{question} [y/N]: ");
    return string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
}

static bool TryParseGermanOrInvariantDouble(string? text, out double value) =>
    double.TryParse(text, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out value) ||
    double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

static string FormatVolume(double? volume) => volume is null
    ? "unknown"
    : $"{volume.Value.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"))} dB";

static string FormatMute(bool? muted) => muted switch
{
    true => "On",
    false => "Off",
    null => "unknown"
};

static string FormatSpeakers(IReadOnlyList<string>? speakers) =>
    speakers is null || speakers.Count == 0
        ? " unknown/none"
        : $" {string.Join(", ", speakers)}";

static void ControlReceiverLogger()
{
    while (true)
    {
        Console.WriteLine($"""
            ── Receiver Log ──
            Path:    {ReceiverLogger.FilePath ?? "(not set)"}
            Logging: {(ReceiverLogger.Enabled ? "enabled" : "disabled")}

            1  Set log file path
            2  Enable logging
            3  Disable logging
            4  Clear log file (overwrite with blank)
            0  Back to main menu
            """);
        Console.Write("Log selection: ");

        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        try
        {
            switch (choice)
            {
                case "0":
                case "":
                case null:
                    return;
                // <-----------
                case "1":
                    Console.Write("Log file path: ");
                    var path = Console.ReadLine()?.Trim();

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        Console.WriteLine("No path entered.");
                        break;
                        // <-----------
                    }

                    ReceiverLogger.FilePath = path;
                    Console.WriteLine($"Log path set: {ReceiverLogger.FilePath}");
                    break;
                // <-----------
                case "2":
                    if (ReceiverLogger.FilePath is null)
                    {
                        Console.WriteLine("Set a log file path first.");
                        break;
                        // <-----------
                    }

                    ReceiverLogger.Enabled = true;
                    Console.WriteLine("Logging enabled.");
                    break;
                // <-----------
                case "3":
                    ReceiverLogger.Enabled = false;
                    Console.WriteLine("Logging disabled.");
                    break;
                // <-----------
                case "4":
                    if (ReceiverLogger.FilePath is null)
                    {
                        Console.WriteLine("Set a log file path first.");
                        break;
                        // <-----------
                    }

                    if (Confirm("Really clear the log file?"))
                    {
                        ReceiverLogger.Clear();
                        Console.WriteLine("Log file cleared.");
                    }

                    break;
                // <-----------
                default:
                    Console.WriteLine("Invalid selection.");
                    break;
                    // <-----------
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            Console.Error.WriteLine($"\nLog error: {exception.Message}");
        }
    }
}