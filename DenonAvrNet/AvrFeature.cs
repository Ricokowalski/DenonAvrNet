namespace DenonAvrNet;

/// <summary>
/// Describes a logical library operation. Use <see cref="DenonAvrClient.GetSupportedProtocols"/>
/// to determine which network transport implements an operation.
/// </summary>
public enum AvrFeature
{
    MainZonePower,
    MainZoneVolume,
    MainZoneMute,
    MainZoneInput,
    MainZoneStatus,
    Zone2Control,
    Zone3Control,
    LiveEvents,
    ChannelLevelRead,
    ChannelLevelControl,
    SpeakerPresetLevelControl,
    SpeakerDistanceControl,
    AudioInformation,
    ActiveSpeakerStatus,
    SpeakerPresetControl,
    SurroundModeControl,
    DigitalInputModeControl,
    /// <summary>Reads and switches the active speaker preset through the receiver's speaker setup web API.</summary>
    SpeakerPresetSelection
}