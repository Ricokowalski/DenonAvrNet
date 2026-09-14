namespace DenonAvrNet.Models;

/// <summary>One speaker level returned by a Denon <c>CV</c> Telnet response.</summary>
public sealed record DenonSpeakerLevel(
    DenonSpeakerLevelChannel Channel,
    double? Decibels,
    bool IsOff,
    string RawResponse);
