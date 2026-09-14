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
    AudioInformation,
    ActiveSpeakerStatus,
    SpeakerPresetControl,
    SurroundModeControl,
    DigitalInputModeControl
}
