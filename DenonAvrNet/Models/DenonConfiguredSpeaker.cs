namespace DenonAvrNet.Models;

/// <summary>
/// One speaker that exists in the receiver's configuration, with its persistent level and distance
/// from the active speaker preset. A value is <see langword="null"/> if the receiver did not return it.
/// </summary>
/// <param name="SpeakerIndex">Speaker index used by the receiver's speaker setup web interface.</param>
/// <param name="Channel">The single channel this index stands for.</param>
/// <param name="LevelDb">Level in dB, or <see langword="null"/> if unavailable.</param>
/// <param name="DistanceMeters">Distance in meters, or <see langword="null"/> if unavailable.</param>
public sealed record DenonConfiguredSpeaker(
    int SpeakerIndex,
    SpeakerChannel Channel,
    double? LevelDb,
    double? DistanceMeters);