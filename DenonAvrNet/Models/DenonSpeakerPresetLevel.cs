namespace DenonAvrNet.Models;

/// <summary>One actual speaker-preset level returned by the receiver web interface.</summary>
public sealed record DenonSpeakerPresetLevel(int SpeakerIndex, double Decibels);
