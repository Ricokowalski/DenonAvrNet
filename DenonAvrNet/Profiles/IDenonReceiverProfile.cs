using DenonAvrNet.Models;

namespace DenonAvrNet.Profiles;

/// <summary>
/// Describes the receiver-specific parts of the HTTP API. A profile is selected from
/// the reported device information; individual feature providers can differ per model.
/// </summary>
internal interface IDenonReceiverProfile
{
    string Id { get; }
    ISpeakerPresetLevelProvider SpeakerPresetLevels { get; }
}

/// <summary>Implements the speaker-preset level API of one receiver family.</summary>
internal interface ISpeakerPresetLevelProvider
{
    Task<IReadOnlyList<DenonSpeakerPresetLevel>> GetLevelsAsync(
        DenonProfileContext context,
        CancellationToken cancellationToken);

    Task SetLevelAsync(
        DenonProfileContext context,
        int speakerIndex,
        double decibels,
        CancellationToken cancellationToken);
}
