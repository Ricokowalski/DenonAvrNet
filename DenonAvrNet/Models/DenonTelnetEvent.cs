namespace DenonAvrNet.Models;

/// <summary>Represents one unsolicited status message received over Denon Telnet.</summary>
public sealed record DenonTelnetEvent(DateTimeOffset Timestamp, string Message)
{
    /// <summary>
    /// Gets the decoded active-speaker matrix for an <c>OPINFASP</c> message,
    /// otherwise <see langword="null"/>.
    /// </summary>
    public DenonActiveSpeakerMatrix? ActiveSpeakerMatrix =>
        DenonTelnetTelemetry.TryParseActiveSpeakerMatrix(Message, out var matrix)
            ? matrix
            : null;
}
