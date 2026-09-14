using System.Globalization;
using System.Text;
using DenonAvrNet;
using DenonAvrNet.Exceptions;

Console.OutputEncoding = Encoding.UTF8;

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

Console.Write("Receiver-IP [10.37.0.190]: ");
var enteredHost = Console.ReadLine()?.Trim();
var host = string.IsNullOrWhiteSpace(enteredHost) ? "10.37.0.190" : enteredHost;

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
        0  Beenden
        """);
    Console.Write("Auswahl: ");
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
    Console.WriteLine($"  Audioformat: {state.Audio?.AudioFormat ?? "unbekannt"}");
    Console.WriteLine($"  Soundmodus:  {state.Audio?.SoundMode ?? "unbekannt"}");
    Console.WriteLine($"  Samplerate:  {state.Audio?.SampleRate ?? "unbekannt"}");
    Console.WriteLine($"  Lautsprecher:{FormatSpeakers(state.Audio?.ActiveSpeakers)}");
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
    var state = await receiver.UpdateAsync(cancellationToken);

    Console.WriteLine("Verfügbare Eingänge:");
    for (var index = 0; index < state.AvailableInputs.Count; index++)
    {
        Console.WriteLine($"  {index + 1,2}: {state.AvailableInputs[index]}");
    }

    Console.Write("Nummer oder Denon-Protokollname: ");
    var selection = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(selection))
    {
        return;
    }

    var input = int.TryParse(selection, out var number) &&
                number >= 1 && number <= state.AvailableInputs.Count
        ? state.AvailableInputs[number - 1]
        : selection;

    await ExecuteAndRefreshAsync(
        receiver,
        token => receiver.SetInputAsync(input, token),
        cancellationToken);
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
