namespace DenonAvrNet.Models;

/// <summary>
/// Stores the persistent speaker-preset levels that were read from the receiver at one point in time.
/// </summary>
public sealed record DenonSpeakerLevelSnapshot(
    DateTimeOffset CreatedAt,
    string ReceiverProfileId,
    IReadOnlyList<DenonSpeakerPresetLevel> Levels);