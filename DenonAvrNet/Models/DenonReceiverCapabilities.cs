namespace DenonAvrNet.Models;

/// <summary>
/// Capabilities detected for one concrete receiver. A nullable value means that the
/// receiver has not yet been probed for that capability; it does not mean unsupported.
/// </summary>
public sealed record DenonReceiverCapabilities(
    bool SupportsHttp,
    bool? SupportsTelnet,
    bool SupportsAppCommand,
    bool? SupportsAppCommand0300,
    bool SupportsZone2,
    bool SupportsZone3,
    int? ZoneCount);
