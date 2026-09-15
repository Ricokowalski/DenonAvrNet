using System.Diagnostics;
using System.Globalization;
using System.Text;
using DenonAvrNet;
using DenonAvrNet.Exceptions;
using DenonAvrNet.Models;

Console.OutputEncoding = Encoding.UTF8;

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

Console.Write("Receiver-IP [192.168.0.5]: ");
var enteredHost = Console.ReadLine()?.Trim();
var host = string.IsNullOrWhiteSpace(enteredHost) ? "192.168.0.5" : enteredHost;

using var receiver = new DenonAvrClient(host);

try
{
    Console.WriteLine($"\nVerbinde mit {host} …");
    var device = await receiver.InitializeAsync(cancellationSource.Token);

    Console.WriteLine("Verbindung hergestellt:");
    Console.WriteLine($"  Modell:      {device.ModelName}");
    Console.WriteLine($"  API-Version: {device.CommunicationApiVersion ?? "unbekannt"}");
    Console.WriteLine($"  Zonen:       {device.ZoneCount?.ToString() ?? "unbekannt"}");
    Console.WriteLine($"  HTTP-Port:   {receiver.HttpPort}");

    await using var monitor = new DenonReceiverMonitor(host);
    monitor.TelnetEventReceived += telnetEvent =>
    {
        if (telnetEvent.ActiveSpeakerMatrix is { } matrix)
        {
            Console.WriteLine(
                $"\n[Telnet {telnetEvent.Timestamp:HH:mm:ss}] Aktive Lautsprecher: " +
                $"{matrix.ActivePositionCount}/{matrix.PositionCount} Ausgangspositionen " +
                $"(Matrix: {matrix.RawValues})");
            return;
        }

        Console.WriteLine($"\n[Telnet {telnetEvent.Timestamp:HH:mm:ss}] {telnetEvent.Message}");
    };
    monitor.Error += exception =>
        Console.Error.WriteLine($"\n[Monitor] {exception.Message}");
    await monitor.StartAsync(cancellationSource.Token);
    Console.WriteLine("Hintergrundmonitor aktiv (Telnet-Ereignisse + Statusabfrage alle 15 Sekunden).");

    await ShowStatusAsync(receiver, cancellationSource.Token);

    while (!cancellationSource.IsCancellationRequested)
    {
        PrintMenu();
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        switch (choice)
        {
            case "1":
                await ShowStatusAsync(receiver, cancellationSource.Token);
                break;
            case "2":
                await ExecuteAndRefreshAsync(receiver, receiver.PowerOnAsync, cancellationSource.Token);
                break;
            case "3":
                if (Confirm("Main Zone wirklich in Standby schalten?"))
                {
                    await receiver.PowerOffAsync(cancellationSource.Token);
                    Console.WriteLine("Standby-Befehl gesendet.");
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
            case "T":
                await ControlAdditionalZonesAsync(host, receiver, cancellationSource.Token);
                break;
            case "A":
                await ControlAudioAndSpeakerPresetsAsync(host, receiver, cancellationSource.Token);
                break;
            case "L":
                await ControlSpeakerLevelsAsync(receiver, cancellationSource.Token);
                break;
            case "0":
            case "Q":
                return;
            default:
                Console.WriteLine("Unbekannte Auswahl.");
                break;
        }
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nAbgebrochen.");
}
catch (DenonAvrException exception)
{
    Console.Error.WriteLine($"\nDenon-Fehler: {exception.Message}");
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine($"\nNetzwerkfehler: {exception.Message}");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"\nUnerwarteter Fehler: {exception}");
}

static void PrintMenu()
{
    Console.WriteLine("""

        ── Denon AVR Testprogramm ──
        1  Status aktualisieren
        2  Main Zone einschalten
        3  Main Zone ausschalten
        4  Lauter
        5  Leiser
        6  Lautstärke setzen
        7  Mute einschalten
        8  Mute ausschalten
        9  Eingang auswählen
        D  Nur-Lese-Diagnose (5 Statusabfragen)
        T  Zone 2/3 anzeigen und schalten (Telnet)
        A  Speaker-Presets und Audio-Modi (Telnet)
        L  Lautsprecher-Kanalpegel (Telnet)
        0  Beenden
        """);
    Console.Write("Auswahl: ");
}

static async Task ControlSpeakerLevelsAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        Console.WriteLine("""

            ── Lautsprecher-Kanalpegel (Telnet) ──
            1  Alle Speaker-Preset-Pegel anzeigen (HTTP)
            2  Kanalpegel direkt setzen
            3  Kanalpegel schrittweise erhöhen
            4  Kanalpegel schrittweise verringern
            5  Subwoofer-Kanal auf OFF setzen
            H  Speaker-Preset-Pegel per HTTP setzen (echte Setup-Werte)
            W  Alle Kanalpegel auf Denon-Werkswerte zurücksetzen
            0  Zurück zum Hauptmenü
            """);
        Console.Write("Pegel-Auswahl: ");
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
                if (Confirm("Wirklich alle Kanalpegel auf Denon-Werkswerte zurücksetzen?"))
                {
                    await receiver.ResetSpeakerLevelsToFactoryDefaultsAsync(
                        DenonControlProtocol.Telnet,
                        cancellationToken);
                    Console.WriteLine("Die Kanalpegel wurden auf Denon-Werkswerte zurückgesetzt.");
                }

                break;
            default:
                Console.WriteLine("Ungültige Auswahl.");
                break;
        }
    }
}

static async Task SetSpeakerPresetLevelFromConsoleAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    Console.Write("Speaker-Index aus der Weboberfläche: ");
    if (!int.TryParse(Console.ReadLine()?.Trim(), out var speakerIndex) || speakerIndex < 0)
    {
        Console.WriteLine("Ungültiger Speaker-Index.");
        return;
    }

    Console.Write("Neuer Speaker-Preset-Pegel (-12,0 bis +12,0 dB): ");
    if (!TryParseGermanOrInvariantDouble(Console.ReadLine()?.Trim(), out var decibels))
    {
        Console.WriteLine("Ungültiger Pegel.");
        return;
    }

    await receiver.SetSpeakerPresetLevelAsync(speakerIndex, decibels, cancellationToken);
    Console.WriteLine($"Speaker-Index {speakerIndex}: {decibels:0.0} dB im Speaker Preset gesetzt.");
}

static async Task ShowSpeakerLevelsAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    var levels = await receiver.GetSpeakerPresetLevelsAsync(cancellationToken);
    if (levels.Count == 0)
    {
        Console.WriteLine("Der Receiver hat keine Kanalpegel zurückgegeben.");
        return;
    }

    Console.WriteLine("Aktive Speaker-Preset-Pegel:");
    foreach (var level in levels)
    {
        Console.WriteLine($"  {(level.Channel?.ToString() ?? "Unbekannt"),-42} {level.Decibels:0.0} dB (Index {level.SpeakerIndex})");
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

    Console.Write("Neuer Pegel (-12,0 bis +12,0 dB, Schritte von 0,5): ");
    if (!TryParseGermanOrInvariantDouble(Console.ReadLine()?.Trim(), out var decibels))
    {
        Console.WriteLine("Ungültiger Pegel.");
        return;
    }

    await receiver.SetSpeakerLevelAsync(channel.Value, decibels, DenonControlProtocol.Telnet, cancellationToken);
    Console.WriteLine($"{channel.Value}: {decibels:0.0} dB gesetzt.");
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
    Console.WriteLine($"{channel.Value}: Pegel {(increase ? "erhöht" : "verringert")}.");
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
    Console.WriteLine($"{channel.Value}: OFF gesetzt.");
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
            ? "Es ist kein konfigurierter Subwoofer-Kanal verfügbar."
            : "Der Receiver hat keine konfigurierten Kanalpegel zurückgegeben.");
        return null;
    }

    Console.WriteLine("Kanal auswählen:");
    for (var index = 0; index < selectableLevels.Length; index++)
    {
        var level = selectableLevels[index];
        Console.WriteLine($"  {index + 1,2}  {level.Channel,-24} {FormatSpeakerLevel(level)}");
    }

    Console.Write("Nummer: ");
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
        : "unbekannt";

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

            1  Zone 2 einschalten       2  Zone 2 ausschalten
            3  Zone 3 einschalten       4  Zone 3 ausschalten
            5  Zone 2 lauter            6  Zone 2 leiser
            7  Zone 3 lauter            8  Zone 3 leiser
            V  Lautstärke direkt setzen
            M  Mute ein-/ausschalten
            E  Eingang auswählen
            0  Zurück zum Hauptmenü
            """);
        Console.Write("Zonen-Auswahl: ");
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
                Console.WriteLine("Ungültige Auswahl.");
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

    while (!cancellationToken.IsCancellationRequested)
    {
        Console.WriteLine("""

            ── Speaker-Presets und Audio ──
            1  Speaker Preset 1
            2  Speaker Preset 2
            3  Surround-/Soundmodus wählen
            4  Digitalen Eingangsdecoder wählen (Auto / PCM / DTS)
            0  Zurück zum Hauptmenü
            """);
        Console.Write("Audio-Auswahl: ");
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        switch (choice)
        {
            case "0":
            case "":
            case null:
                return;
            case "1":
                await ExecuteAudioCommandAsync(
                    () => telnet.SelectSpeakerPresetAsync(1, cancellationToken),
                    receiver,
                    cancellationToken,
                    800);
                break;
            case "2":
                await ExecuteAudioCommandAsync(
                    () => telnet.SelectSpeakerPresetAsync(2, cancellationToken),
                    receiver,
                    cancellationToken,
                    800);
                break;
            case "3":
                await SetSurroundModeAsync(telnet, receiver, cancellationToken);
                break;
            case "4":
                await SetDigitalInputModeAsync(telnet, receiver, cancellationToken);
                break;
            default:
                Console.WriteLine("Ungültige Auswahl.");
                break;
        }
    }
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
    Console.Write("Soundmodus: ");
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
        Console.WriteLine("Ungültige Auswahl.");
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
    Console.Write("Digitaler Eingangsdecoder [A]uto / [P]CM / [D]TS: ");
    var mode = Console.ReadLine()?.Trim().ToUpperInvariant() switch
    {
        "A" => "Auto",
        "P" => "PCM",
        "D" => "DTS",
        _ => null
    };

    if (mode is null)
    {
        Console.WriteLine("Bitte A, P oder D eingeben.");
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
        Console.WriteLine($"Receiver-Antwort: {response}");
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

    Console.WriteLine("\nAktueller Zonenstatus:");
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

    Console.Write("Lautstärke in dB (-80,0 bis +18,0): ");
    if (!TryParseGermanOrInvariantDouble(Console.ReadLine(), out var volume))
    {
        Console.WriteLine("Ungültige Zahl.");
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

    Console.Write("Mute [E]in/[A]us: ");
    var selection = Console.ReadLine()?.Trim().ToUpperInvariant();
    if (selection is not ("E" or "A"))
    {
        Console.WriteLine("Bitte E oder A eingeben.");
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
    Console.WriteLine("Verfügbare Eingänge:");
    for (var index = 0; index < inputs.Count; index++)
    {
        Console.WriteLine($"  {index + 1,2}: {inputs[index]}");
    }

    Console.Write("Nummer oder Denon-Protokollname: ");
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

    Console.WriteLine("Bitte nur 2 oder 3 eingeben.");
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

    Console.WriteLine("\nAktueller Main-Zone-Status:");
    Console.WriteLine($"  Power:       {state.Power}");
    Console.WriteLine($"  Eingang:     {state.Input ?? "unbekannt"}");
    Console.WriteLine($"  Lautstärke:  {FormatVolume(state.VolumeDb)}");
    Console.WriteLine($"  Mute:        {FormatMute(state.IsMuted)}");
    Console.WriteLine($"  Audio-Eingang:{state.Audio?.InputMode ?? "unbekannt"}");
    Console.WriteLine($"  Audioformat: {state.Audio?.AudioFormat ?? "unbekannt"}");
    Console.WriteLine($"  Soundmodus:  {state.Audio?.SoundMode ?? "unbekannt"}");
    Console.WriteLine($"  Samplerate:  {state.Audio?.SampleRate ?? "unbekannt"}");
    Console.WriteLine($"  Lautsprecher:{FormatSpeakers(state.Audio?.ActiveSpeakers)}");
    ShowAdditionalZone("Zone 2", state.Zone2);
    ShowAdditionalZone("Zone 3", state.Zone3);
}

static void ShowAdditionalZone(string name, DenonAvrNet.Models.DenonZoneState? zone)
{
    if (zone is not null)
    {
        Console.WriteLine($"  {name}:       {zone.Power}, {zone.Input ?? "unbekannt"}, {FormatVolume(zone.VolumeDb)}, Mute {FormatMute(zone.IsMuted)}");
    }
}

static async Task SetVolumeAsync(
    DenonAvrClient receiver,
    CancellationToken cancellationToken)
{
    Console.Write("Lautstärke in dB (-80,0 bis +18,0): ");
    var text = Console.ReadLine();

    if (!TryParseGermanOrInvariantDouble(text, out var volume))
    {
        Console.WriteLine("Ungültige Zahl.");
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

    Console.WriteLine("Verfügbare Eingänge:");
    for (var index = 0; index < inputs.Count; index++)
    {
        Console.WriteLine($"  {index + 1,2}: {inputs[index]}");
    }

    Console.Write("Nummer oder Denon-Protokollname: ");
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
        $"\nStarte {repetitions} reine Statusabfragen; es werden keine Steuerbefehle gesendet.");

    for (var attempt = 1; attempt <= repetitions; attempt++)
    {
        var stopwatch = Stopwatch.StartNew();
        var state = await receiver.UpdateAsync(cancellationToken);
        stopwatch.Stop();

        Console.WriteLine(
            $"  {attempt}/{repetitions}  {stopwatch.ElapsedMilliseconds,4} ms | " +
            $"Power {state.Power} | Eingang {state.Input ?? "?"} | " +
            $"Lautstärke {FormatVolume(state.VolumeDb)} | " +
            $"Audio {state.Audio?.AudioFormat ?? "?"} | " +
            $"Lautsprecher{FormatSpeakers(state.Audio?.ActiveSpeakers)}");

        if (attempt < repetitions)
        {
            await Task.Delay(500, cancellationToken);
        }
    }

    Console.WriteLine("Nur-Lese-Diagnose abgeschlossen.");
}

static bool Confirm(string question)
{
    Console.Write($"{question} [j/N]: ");
    return string.Equals(Console.ReadLine()?.Trim(), "j", StringComparison.OrdinalIgnoreCase);
}

static bool TryParseGermanOrInvariantDouble(string? text, out double value) =>
    double.TryParse(text, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out value) ||
    double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

static string FormatVolume(double? volume) => volume is null
    ? "unbekannt"
    : $"{volume.Value.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"))} dB";

static string FormatMute(bool? muted) => muted switch
{
    true => "Ein",
    false => "Aus",
    null => "unbekannt"
};

static string FormatSpeakers(IReadOnlyList<string>? speakers) =>
    speakers is null || speakers.Count == 0
        ? " unbekannt/keine"
        : $" {string.Join(", ", speakers)}";
