namespace DenonAvrNet.Models;

public sealed record DenonReceiverState(
    bool IsPoweredOn,
    string Power,
    string? Input,
    double? VolumeDb,
    bool? IsMuted,
    IReadOnlyList<string> AvailableInputs);
