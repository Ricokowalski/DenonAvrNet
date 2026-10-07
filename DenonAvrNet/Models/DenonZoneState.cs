namespace DenonAvrNet.Models;

/// <summary>Represents an immutable status snapshot of a non-Main receiver zone.</summary>
public sealed record DenonZoneState(
    bool IsPoweredOn,
    string Power,
    string? Input,
    double? VolumeDb,
    bool? IsMuted)
{
    /// <summary>Gets the same volume on Denon's absolute 0..98 scale.</summary>
    public double? Volume => VolumeDb is null ? null : VolumeDb.Value + 80.0;
}
