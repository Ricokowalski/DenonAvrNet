namespace DenonAvrNet.Models;

/// <summary>One speaker distance returned by the receiver's speaker setup interface.</summary>
public sealed record DenonSpeakerDistance(int SpeakerIndex, double Meters)
{
    /// <summary>Typed receiver channel resolved from <see cref="SpeakerIndex"/>, if known.</summary>
    public SpeakerChannel? Channel =>
        DenonSpeakerDistanceIndexConverter.TryToSpeakerChannel(SpeakerIndex, out var channel)
            ? channel
            : null;
}