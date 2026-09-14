namespace DenonAvrNet.Models;

/// <summary>Describes the audio signal and speaker activity reported by the receiver.</summary>
public sealed record DenonAudioInfo(
    string? InputMode,
    string? Output,
    string? AudioFormat,
    string? SoundMode,
    string? SampleRate,
    IReadOnlyList<string> ActiveSpeakers)
{
    /// <summary>
    /// Gets the active speakers as strongly typed flags. The original receiver
    /// codes remain available through <see cref="ActiveSpeakers"/>.
    /// </summary>
    public SpeakerChannel ActiveSpeakerChannels => SpeakerChannelParser.Parse(ActiveSpeakers);
}
