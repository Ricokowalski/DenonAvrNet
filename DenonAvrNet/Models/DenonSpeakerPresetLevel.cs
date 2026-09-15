namespace DenonAvrNet.Models;

/// <summary>One actual speaker-preset level returned by the receiver web interface.</summary>
public sealed record DenonSpeakerPresetLevel(int SpeakerIndex, double Decibels)
{
    /// <summary>Typed receiver channel resolved from <see cref="SpeakerIndex"/>, if known.</summary>
    public SpeakerChannel? Channel =>
        DenonSpeakerPresetIndexConverter.TryToSpeakerChannel(SpeakerIndex, out var channel)
            ? channel
            : null;
}
