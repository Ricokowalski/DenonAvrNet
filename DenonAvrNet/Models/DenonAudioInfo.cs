namespace DenonAvrNet.Models;

/// <summary>Describes the audio signal and speaker activity reported by the receiver.</summary>
public sealed record DenonAudioInfo(
    string? InputMode,
    string? Output,
    string? AudioFormat,
    string? SoundMode,
    string? SampleRate,
    IReadOnlyList<string> ActiveSpeakers);
