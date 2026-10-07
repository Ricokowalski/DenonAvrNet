using System.Globalization;
using System.Xml.Linq;
using DenonAvrNet.Models;
using DenonAvrNet.Protocol;

namespace DenonAvrNet.Profiles;

/// <summary>Speaker-distance API used by the AVC-X6800H web interface.</summary>
internal sealed class AjaxSpeakerDistanceProvider : ISpeakerDistanceProvider
{
    private const int SpeakerSetupHttpPort = 11080;

    public async Task<DenonSpeakerDistanceConfiguration> GetDistancesAsync(
        DenonProfileContext context,
        CancellationToken cancellationToken)
    {
        var response = await context.HttpTransport.GetStringAsync(
            context.Host,
            SpeakerSetupHttpPort,
            DenonEndpoints.SpeakerDistances(),
            cancellationToken).ConfigureAwait(false);

        var document = XDocument.Parse(response);
        var distances = document.Descendants("Distances").FirstOrDefault()
            ?? throw new InvalidDataException(
                "Die Receiver-Antwort enthält kein Distances-Element.");

        var unitValue = ParseRequiredInt(
            distances.Element("Unit"),
            "Unit");

        var unit = unitValue switch
        {
            1 => DenonSpeakerDistanceUnit.Meters,
            2 => DenonSpeakerDistanceUnit.Feet,
            _ => throw new InvalidDataException(
                $"Unbekannte Denon-Distanzeinheit: {unitValue}.")
        };

        var rawStep = ParseRequiredInt(
            distances.Element("Step"),
            "Step");

        var ratio = TryParseInt(
            distances.Element("M2FConvertRatio"));

        var speakers = distances
            .Descendants("Speaker")
            .Select(element => new
            {
                Index = (int?)element.Attribute("index"),
                RawValue = TryParseInt(element)
            })
            .Where(item =>
                item.Index is not null &&
                item.RawValue is not null)
            .Select(item => new DenonSpeakerDistance(
                item.Index!.Value,
                RawToMeters(item.RawValue!.Value)))
            .OrderBy(distance => distance.SpeakerIndex)
            .ToArray();

        return new DenonSpeakerDistanceConfiguration(
            unit,
            RawToMeters(rawStep),
            ratio,
            speakers);
        // <-----------
    }

    public async Task SetDistanceAsync(
        DenonProfileContext context,
        int speakerIndex,
        double meters,
        CancellationToken cancellationToken)
    {
        // The receiver reports raw values as meters x 100 in both unit modes. The read is kept
        // so the response is still validated before writing.
        _ = await GetDistancesAsync(
            context,
            cancellationToken).ConfigureAwait(false);

        var rawValue = MetersToRaw(meters);

        _ = await context.HttpTransport.GetStringAsync(
            context.Host,
            SpeakerSetupHttpPort,
            DenonEndpoints.SetSpeakerDistance(
                speakerIndex,
                rawValue),
            cancellationToken).ConfigureAwait(false);
    }

    private static double RawToMeters(int rawValue) =>
        rawValue / 100.0;

    private static int MetersToRaw(double meters) =>
        (int)Math.Round(
            meters * 100.0,
            MidpointRounding.AwayFromZero);

    private static int ParseRequiredInt(
        XElement? element,
        string name) =>
        TryParseInt(element)
        ?? throw new InvalidDataException(
            $"Die Receiver-Antwort enthält keinen gültigen {name}-Wert.");

    private static int? TryParseInt(XElement? element) =>
        element is not null &&
        int.TryParse(
            element.Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
}

internal sealed class UnsupportedSpeakerDistanceProvider(
    string receiverDescription)
    : ISpeakerDistanceProvider
{
    public Task<DenonSpeakerDistanceConfiguration> GetDistancesAsync(
        DenonProfileContext context,
        CancellationToken cancellationToken) =>
        Task.FromException<DenonSpeakerDistanceConfiguration>(
            CreateException());

    public Task SetDistanceAsync(
        DenonProfileContext context,
        int speakerIndex,
        double meters,
        CancellationToken cancellationToken) =>
        Task.FromException(
            CreateException());

    private NotSupportedException CreateException() => new(
        $"Speaker-distance control has not yet been implemented for {receiverDescription}. " +
        "Capture the speaker-distance page requests and add a dedicated provider for this receiver profile.");
}