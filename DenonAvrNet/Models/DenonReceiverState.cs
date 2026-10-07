namespace DenonAvrNet.Models;

/// <summary>Represents an immutable, confirmed Main Zone status snapshot.</summary>
public sealed record DenonReceiverState(
    bool IsPoweredOn,
    string Power,
    string? Input,
    double? VolumeDb,
    bool? IsMuted,
    IReadOnlyList<string> AvailableInputs,
    DenonAudioInfo? Audio = null,
    DenonZoneState? Zone2 = null,
    DenonZoneState? Zone3 = null)
{
    /// <summary>Gets the same Main Zone volume on Denon's absolute 0..98 scale.</summary>
    public double? Volume => VolumeDb is null ? null : VolumeDb.Value + 80.0;
}
