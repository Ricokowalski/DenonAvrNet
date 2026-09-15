using System.Globalization;
using System.Xml.Linq;
using DenonAvrNet.Models;
using DenonAvrNet.Protocol;

namespace DenonAvrNet.Profiles;

/// <summary>Speaker-preset API used by the AVC-X6800H web interface.</summary>
internal sealed class AjaxSpeakerPresetLevelProvider : ISpeakerPresetLevelProvider
{
    private const int SpeakerSetupHttpPort = 11080;

    public async Task<IReadOnlyList<DenonSpeakerPresetLevel>> GetLevelsAsync(
        DenonProfileContext context,
        CancellationToken cancellationToken)
    {
        var response = await context.HttpTransport.GetStringAsync(
            context.Host,
            SpeakerSetupHttpPort,
            DenonEndpoints.SpeakerPresetLevels(),
            cancellationToken).ConfigureAwait(false);

        var document = XDocument.Parse(response);
        return document.Descendants("Speaker")
            .Select(element => new
            {
                Index = (int?)element.Attribute("index"),
                Value = element.Value
            })
            .Where(item => item.Index is not null &&
                           int.TryParse(item.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Select(item => new DenonSpeakerPresetLevel(
                item.Index!.Value,
                int.Parse(item.Value, CultureInfo.InvariantCulture) / 10.0))
            .OrderBy(level => level.SpeakerIndex)
            .ToArray();
    }

    public async Task SetLevelAsync(
        DenonProfileContext context,
        int speakerIndex,
        double decibels,
        CancellationToken cancellationToken)
    {
        var tenthsOfDecibels = (int)Math.Round(decibels * 10, MidpointRounding.AwayFromZero);
        _ = await context.HttpTransport.GetStringAsync(
            context.Host,
            SpeakerSetupHttpPort,
            DenonEndpoints.SetSpeakerPresetLevel(speakerIndex, tenthsOfDecibels),
            cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class UnsupportedSpeakerPresetLevelProvider(string receiverDescription)
    : ISpeakerPresetLevelProvider
{
    public Task<IReadOnlyList<DenonSpeakerPresetLevel>> GetLevelsAsync(
        DenonProfileContext context,
        CancellationToken cancellationToken) =>
        Task.FromException<IReadOnlyList<DenonSpeakerPresetLevel>>(CreateException());

    public Task SetLevelAsync(
        DenonProfileContext context,
        int speakerIndex,
        double decibels,
        CancellationToken cancellationToken) =>
        Task.FromException(CreateException());

    private NotSupportedException CreateException() => new(
        $"Speaker-preset level control has not yet been implemented for {receiverDescription}. " +
        "Capture the speaker-level page requests and add a dedicated provider for this receiver profile.");
}
