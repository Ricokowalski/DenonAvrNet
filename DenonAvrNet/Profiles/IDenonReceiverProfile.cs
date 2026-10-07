using DenonAvrNet.Models;

namespace DenonAvrNet.Profiles;

/// <summary>
/// Describes the receiver-specific parts of the HTTP API. A profile is selected from
/// the reported device information; individual feature providers can differ per model.
/// </summary>
internal interface IDenonReceiverProfile
{
    string Id { get; }
    int SpeakerPresetCount { get; }
    bool SupportsSpeakerPresetLevels { get; }
    bool SupportsSpeakerDistances { get; }
    bool SupportsSpeakerPresetSelection { get; }
    ISpeakerPresetLevelProvider SpeakerPresetLevels { get; }
    ISpeakerDistanceProvider SpeakerDistances { get; }
    ISpeakerPresetSelectionProvider SpeakerPresetSelection { get; }
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

/// <summary>Implements the persistent speaker-distance API of one receiver family.</summary>
internal interface ISpeakerDistanceProvider
{
    Task<DenonSpeakerDistanceConfiguration> GetDistancesAsync(
        DenonProfileContext context,
        CancellationToken cancellationToken);

    Task SetDistanceAsync(
        DenonProfileContext context,
        int speakerIndex,
        double meters,
        CancellationToken cancellationToken);
}

/// <summary>Implements reading and switching of the active speaker preset of one receiver family.</summary>
internal interface ISpeakerPresetSelectionProvider
{
    Task<int> GetActivePresetAsync(
        DenonProfileContext context,
        CancellationToken cancellationToken);

    Task SelectPresetAsync(
        DenonProfileContext context,
        int preset,
        TimeSpan confirmationTimeout,
        CancellationToken cancellationToken);
}