namespace DenonAvrNet.Models;

/// <summary>Distance unit currently selected in the receiver's speaker setup.</summary>
public enum DenonSpeakerDistanceUnit
{
    Meters = 1,
    Feet = 2
}

/// <summary>
/// Speaker-distance configuration returned by the receiver. Public distance values are always
/// normalized to meters, independently of the unit selected in the receiver UI.
/// </summary>
public sealed record DenonSpeakerDistanceConfiguration(
    DenonSpeakerDistanceUnit ReceiverUnit,
    double Step,
    int? M2FConvertRatio,
    IReadOnlyList<DenonSpeakerDistance> Speakers);