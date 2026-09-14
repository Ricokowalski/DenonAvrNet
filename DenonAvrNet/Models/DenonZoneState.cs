namespace DenonAvrNet.Models;

/// <summary>Represents an immutable status snapshot of a non-Main receiver zone.</summary>
public sealed record DenonZoneState(
    bool IsPoweredOn,
    string Power,
    string? Input,
    double? VolumeDb,
    bool? IsMuted);
