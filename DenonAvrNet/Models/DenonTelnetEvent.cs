namespace DenonAvrNet.Models;

/// <summary>Represents one unsolicited status message received over Denon Telnet.</summary>
public sealed record DenonTelnetEvent(DateTimeOffset Timestamp, string Message);
